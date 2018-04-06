using Guytp.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Guytp.Networking
{
    public abstract class NetworkClient : IDisposable
    {
        private readonly IPAddress _ipAddress;

        private readonly int _port;
        private readonly bool _sslAllowRemoteCertificateChainErrors;
        private bool _isAlive;

        private Thread _thread;
        private readonly string _sslServername;

        /// <summary>
        /// Defines the socket we are using.
        /// </summary>
        private TcpClient _client;
        NetworkConnection _connection = null;


        protected abstract Dictionary<Type, MessageHandlerDelegate> MessageHandlers { get; }

        public ConnectionConfig ConnectionConfig { get; private set; }

        protected NetworkMessageQueue MessageQueue => _connection.OutboundMessageQueue;
        public bool IsConnected { get; private set; }

        public event EventHandler Connected;
        public event EventHandler Disconnected;
        //public event EventHandler<ConnectionInvalidatedEventArgs> Invalidated;
        //public event EventHandler<ConnectionErrorEventArgs> ConnectionError;

        protected NetworkClient(IPAddress ipAddress, int port, string sslServername = null, bool sslAllowRemoteCertificateChainErrors = false)
        {
            _ipAddress = ipAddress;
            _port = port;
            _sslServername = sslServername;
            _sslAllowRemoteCertificateChainErrors = sslAllowRemoteCertificateChainErrors;
            ConnectionConfig = new ConnectionConfig();
        }

        public void Start()
        {
            _isAlive = true;
            _thread = new Thread(MainThread) { Name = GetType().Name + " Network Client", IsBackground = false };
            _thread.Start();
        }

        public void Stop()
        {
            _isAlive = false;
            _thread?.Join();
            _thread = null;
        }



        /// <summary>
        /// Ensures we are connected otherwise an exception is thrown.  This will attemp to connect if no existing socket otherwise it will acssume existing socket is OK.
        /// </summary>
        private void EnsureConnection()
        {
            if (_client != null)
                return;
            _client = new TcpClient();
            _client.Connect(_ipAddress, _port);
            _connection = _sslServername != null ? new NetworkConnection(_client, _sslServername, _sslAllowRemoteCertificateChainErrors) : new NetworkConnection(_client);
            _connection.Config = ConnectionConfig;
            IsConnected = true;
            Connected?.Invoke(this, new EventArgs());
            Logger.ApplicationInstance.Debug("Connected to " + _ipAddress + ":" + _port);
        }

        private void MainThread()
        {
            // Attempt to connect
            Logger.ApplicationInstance.Debug("Starting network client thread");
            while (_isAlive)
            {
                try
                {
                    // If we're invalidated, dispose of that socket now
                    if (_connection != null && _connection.IsInvalidated)
                    {
                        try
                        {
                            IsConnected = false;
                            _connection.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Logger.ApplicationInstance.Error("Failed to dispose of a broken connection", ex);
                        }
                        finally
                        {
                            _connection = null;
                            _client = null;
                            Disconnected?.Invoke(this, new EventArgs());
                        }
                    }

                    // Ensure we have a valid socket and connection before continuing
                    try
                    {
                        EnsureConnection();
                    }
                    catch (Exception ex)
                    {
                        // Some error connecting, wait and try again
                        try
                        {
                            _connection?.Dispose();
                        }
                        catch
                        {
                            // Intentionally swallowed
                        }
                        _client = null;
                        _connection = null;
                        Logger.ApplicationInstance.Error("Failed to connect to remote host", ex);
                        DateTime waitUntil = DateTime.UtcNow.AddSeconds(5);
                        while (DateTime.UtcNow < waitUntil && _isAlive)
                            Thread.Sleep(50);
                        continue;
                    }

                    // We have a valid socket here, so do normal processing
                    try
                    {
                        // Read a request from this connection
                        try
                        {
                            _connection.PerformReadCycle();
                        }
                        catch (Exception ex)
                        {
                            _connection.Invalidate("Error reading data from connection");
                            Logger.ApplicationInstance.Error("Error reading data from connection", ex);
                            continue;
                        }

                        // Now try to process any messages held by this connection
                        try
                        {
                            object queuedMessage = _connection.DequeueReceivedMessage();
                            if (queuedMessage != null)
                            {
                                Type messageType = queuedMessage.GetType();
                                if (MessageHandlers.ContainsKey(messageType))
                                    MessageHandlers[messageType](queuedMessage, _connection.OutboundMessageQueue);
                                else
                                    throw new Exception("Unknown message " + messageType.Name);
                            }
                        }
                        catch (Exception ex)
                        {
                            _connection.Invalidate("Exception handling/dequeueing message");
                            Logger.ApplicationInstance.Error("Error handling/dequeueing message read", ex);
                            continue;
                        }

                        // Perform ping checks
                        try
                        {
                            _connection.PerformPingCheck();
                        }
                        catch (Exception ex)
                        {
                            _connection.Invalidate("Exception attempting to ping check client");
                            Logger.ApplicationInstance.Error("Error with ping check", ex);
                            continue;
                        }

                        // Flush client
                        try
                        {
                            _connection.PerformWriteCycle();
                        }
                        catch (Exception ex)
                        {
                            _connection.Invalidate("Exception attempting to write to client");
                            Logger.ApplicationInstance.Error("Error performing write cycle to client", ex);
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.ApplicationInstance.Error("Fatal error in network client", ex);
                    }
                }
                catch (Exception ex)
                {
                    Logger.ApplicationInstance.Error("Fatal error that was not caught by network client", ex);
                }
                Thread.Sleep(10);
            }

            //  Disconnect
            try
            {
                IsConnected = false;
                _connection.Dispose();
            }
            catch (Exception ex)
            {
                Logger.ApplicationInstance.Error("Failed to shutdown socket", ex);
            }
            finally
            {
                _connection = null;
                _client = null;
                Disconnected?.Invoke(this, new EventArgs());
            }
            Logger.ApplicationInstance.Debug("Finished network client thread");
        }

        public void Dispose()
        {
            Stop();
        }
    }
}