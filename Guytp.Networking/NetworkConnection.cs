using Guytp.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;

namespace Guytp.Networking
{
    internal class NetworkConnection : IDisposable
    {
        private Socket _socket;

        private readonly BinaryFormatter _formatter = new BinaryFormatter
        {
            AssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple,
            TypeFormat = System.Runtime.Serialization.Formatters.FormatterTypeStyle.TypesWhenNeeded
        };

        private readonly Random _random = new Random();
        private byte[] _packetSizeBuffer = new byte[4];

        private byte[] _readbuffer = new byte[102400];

        private List<byte> _fullBuffer = new List<byte>(102400);
        private int _packetSize;
        private Queue<object> _inboundMessages = new Queue<object>();
        private readonly object _isSendingLocker = new object();
        private bool _isSending;
        private bool _isReadingPingRequest;
        private bool _isPingResponseWriteDue;
        private int _lastPingRequestReceivedId;
        private DateTime _pingRequestSendTime;

        public bool IsInvalidated { get; private set; }

        public NetworkMessageQueue OutboundMessageQueue { get; private set; }

        public string InvalidationReason { get; private set; }

        public ConnectionConfig Config { get; private set; }

        public DateTime LastDataReceived { get; private set; }

        public DateTime LastDataSent { get; private set; }

        private bool _isReadingPingResponse;

        private bool _isAwaitingPingResponse;

        private int _expectedPingResponseId;
        private bool _isPingRequestWriteDue;

        public NetworkConnection(Socket socket)
        {
            LastDataReceived = DateTime.UtcNow;
            LastDataSent = DateTime.UtcNow;
            Config = new ConnectionConfig();
            OutboundMessageQueue = new NetworkMessageQueue();
            _socket = socket;
        }

        /// <summary>
        /// Reads pending data for up to a single message.  If a partial read is executed it can be continuede by a subsequent call.  This method should be called multiple times until false is returnede to read the full inbound queue.
        /// </summary>
        /// <returns>
        /// True if there was a command read, otherwis false.
        /// </returns>
        public bool PerformReadCycle()
        {
            // Return if invalidated
            if (IsInvalidated)
                return false;

            // If we're not reading a packet, determine packet size
            int bytesRead;
            if (_packetSize == 0)
            {
                // We need 4 bytes to determine data length to read, without that we wait
                if (_socket.Available < 4)
                    return false;

                // Determine how big the packet is
                if ((bytesRead = _socket.Receive(_packetSizeBuffer, 4, SocketFlags.None)) != 4)
                {
                    Invalidate("Expected 4 bytes but got " + bytesRead + " determining packet length");
                    return false;
                }
                _packetSize = BitConverter.ToInt32(_packetSizeBuffer, 0);

                // First let's handle special cases - a ping request has a packet size of -1
                if (_packetSize == -1)
                {
                    // If we don't support it invalidate the connection now
                    if (!Config.PingCheckResponsesEnabled)
                    {
                        Invalidate("Ping requested from remote side but not supported by this connection");
                        return false;
                    }

                    // Setup that we are reading a 4-byte ping identifier
                    _isReadingPingRequest = true;
                    _packetSize = 4;
                }
                else if (_packetSize == -2)
                {
                    // This indicates a ping response - so read out the response ID
                    if (!Config.PingCheckEnabled)
                    {
                        Invalidate("Ping response from remote side but we don't check responses");
                        return false;
                    }
                    if (!_isAwaitingPingResponse)
                    {
                        Invalidate("Ping response from other side, but not expected");
                        return false;
                    }
                    _isReadingPingResponse = true;
                    _packetSize = 4;
                }

                if (_packetSize > Config.MaximumInboundPacketSize || _packetSize < 1)
                {
                    Invalidate("Packet of " + _packetSize + " outside of valid bounds");
                    return false;
                }
            }

            // Is the inbound message queue already full?
            if (_inboundMessages.Count > Config.MaximumInboundQueueSize)
            {
                Invalidate("Inbound message queue is full for this connection");
                return false;
            }

            // Read as much data as possible, up to packet size
            int available = _socket.Available;
            int remainingRead = _packetSize - _fullBuffer.Count;
            int dataToRead = available >= remainingRead ? remainingRead : available;
            while (dataToRead > 0)
            {
                int readThisLoop = dataToRead < _readbuffer.Length ? dataToRead : _readbuffer.Length;
                bytesRead = _socket.Receive(_readbuffer, readThisLoop, SocketFlags.None);
                LastDataReceived = DateTime.UtcNow;
                if (bytesRead != readThisLoop)
                {
                    Invalidate("Packet size mismatch between required read of " + readThisLoop + " and " + bytesRead + " read");
                    return false;
                }
                if (bytesRead == _readbuffer.Length)
                    _fullBuffer.AddRange(_readbuffer);
                else
                    for (int i = 0; i < bytesRead; i++)
                        _fullBuffer.Add(_readbuffer[i]);
                dataToRead -= bytesRead;
            }

            // Have we read all of it if not we have to come back later?
            if (_fullBuffer.Count != _packetSize)
                return false;

            // If we're reading a ping we do things a little differently
            byte[] fullBuffer = _fullBuffer.ToArray();
            if (_isReadingPingRequest)
            {
                // Read out ping ID and add it to list of ping IDs that we're responding to
                _lastPingRequestReceivedId = BitConverter.ToInt32(fullBuffer, 0);
                _isPingResponseWriteDue = true;
                _isReadingPingRequest = false;
            }
            else if (_isReadingPingResponse)
            {
                int receivedId = BitConverter.ToInt32(fullBuffer, 0);
                if (_expectedPingResponseId != receivedId)
                {
                    Invalidate("Ping response ID of " + receivedId + " did not match " + _expectedPingResponseId);
                    return false;
                }
                _isAwaitingPingResponse = false;
                _isReadingPingResponse = false;
            }
            else
                // Let's deserialise this object
                using (MemoryStream strm = new MemoryStream())
                {
                    strm.Write(fullBuffer, 0, fullBuffer.Length);
                    strm.Flush();
                    strm.Position = 0;
                    object o = _formatter.Deserialize(strm);
                    Logger.ApplicationInstance.Debug("Received message of type " + o.GetType().Name);
                    _inboundMessages.Enqueue(o);
                }

            // Reset for fresh read
            _packetSize = 0;
            _fullBuffer.Clear();
            return true;
        }

        public object DequeueReceivedMessage()
        {
            if (IsInvalidated)
                return null;
            if (_inboundMessages.Count < 1)
                return null;
            object obj = _inboundMessages.Dequeue();
            Logger.ApplicationInstance.Debug("Dequeued received message of " + obj.GetType().Name);
            return obj;
        }

        public void PerformPingCheck()
        {
            if (IsInvalidated)
                return;

            if (!Config.PingCheckEnabled)
                return;

            if (_isPingRequestWriteDue)
                return;

            // Have we already sent a ping request to remote end?
            if (_isAwaitingPingResponse)
            {
                if (DateTime.UtcNow.Subtract(_pingRequestSendTime) > Config.PingResponseTime)
                    Invalidate("Ping response not receiveed in valid window");
                return;
            }

            // Do we need to send one?
            if (DateTime.UtcNow.Subtract(LastDataReceived) > Config.PingCheckInactivityTime)
                _isPingRequestWriteDue = true;
        }

        public void PerformWriteCycle()
        {
            // Return if invalidated
            if (IsInvalidated)
                return;

            // Invalidate if exceeds our limitstele
            if (OutboundMessageQueue.QueueCount > Config.MaximumOutboundQueueSize)
            {
                Invalidate("Outbound queue size exceeded");
                return;
            }

            // Get all queued objects
            object message = OutboundMessageQueue.DequeueMessage();
            if (message == null && !_isPingResponseWriteDue && !_isPingRequestWriteDue)
                return;

            // Skip if already sending
            if (_isSending)
                return;
            lock (_isSendingLocker)
            {
                if (_isSending)
                    return;
                _isSending = true;
            }

            // Serialize message if present
            byte[] messageBuffer = null;
            byte[] intConversionBuffer;
            int offset = 0;
            if (message != null)
            {
                using (MemoryStream strm = new MemoryStream())
                {
                    _formatter.Serialize(strm, message);
                    strm.Flush();
                    messageBuffer = strm.GetBuffer();
                }
            }

            // Send any outbound data
            byte[] outputBuffer = new byte[(messageBuffer == null ? 0 : messageBuffer.Length + 4) + (_isPingResponseWriteDue ? 8 : 0) + (_isPingRequestWriteDue ? 8 : 0)];
            if (_isPingResponseWriteDue)
            {
                intConversionBuffer = BitConverter.GetBytes(-2);
                Array.Copy(intConversionBuffer, 0, outputBuffer, offset, 4);
                offset += 4;
                intConversionBuffer = BitConverter.GetBytes(_lastPingRequestReceivedId);
                Array.Copy(intConversionBuffer, 0, outputBuffer, offset, 4);
                offset += 4;
                Logger.ApplicationInstance.Debug("Sending ping response with ID " + _lastPingRequestReceivedId);
                _isPingResponseWriteDue = false;
            }
            if (_isPingRequestWriteDue)
            {
                _expectedPingResponseId = _random.Next(Int32.MinValue, Int32.MaxValue);
                _pingRequestSendTime = DateTime.UtcNow;
                _isAwaitingPingResponse = true;
                _isPingRequestWriteDue = false;
                intConversionBuffer = BitConverter.GetBytes(-1);
                Array.Copy(intConversionBuffer, 0, outputBuffer, offset, 4);
                offset += 4;
                intConversionBuffer = BitConverter.GetBytes(_expectedPingResponseId);
                Array.Copy(intConversionBuffer, 0, outputBuffer, offset, 4);
                offset += 4;
                Logger.ApplicationInstance.Debug("Sending ping request with ID " + _expectedPingResponseId);
                _isPingRequestWriteDue = false;
            }
            if (messageBuffer != null)
            {
                intConversionBuffer = BitConverter.GetBytes(messageBuffer.Length);
                Array.Copy(intConversionBuffer, 0, outputBuffer, offset, 4);
                offset += 4;
                Array.Copy(messageBuffer, 0, outputBuffer, offset, messageBuffer.Length);
            }
            _socket.BeginSend(outputBuffer, 0, outputBuffer.Length, SocketFlags.None, new AsyncCallback(SocketSendCallback), null);
        }

        private void SocketSendCallback(IAsyncResult ar)
        {
            lock (_isSendingLocker)
                _isSending = false;
            LastDataSent = DateTime.UtcNow;
        }

        public void Invalidate(string reason)
        {
            if (IsInvalidated)
                return;
            Logger.ApplicationInstance.Warn("Connection invalidated: " + reason);
            IsInvalidated = true;
            InvalidationReason = reason;
        }

        public void Dispose()
        {
            _readbuffer = null;
            _fullBuffer = null;
            _packetSizeBuffer = null;
            if (_socket != null)
            {
                List<Exception> exceptions = new List<Exception>();
                try
                {
                    _socket.Shutdown(SocketShutdown.Both);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
                try
                {
                    _socket.Disconnect(false);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
                try
                {
                    _socket.Dispose();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
                _socket = null;
                if (exceptions.Count > 0)
                    throw new AggregateException(exceptions.ToArray());
            }
        }
    }
}