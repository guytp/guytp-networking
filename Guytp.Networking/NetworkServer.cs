﻿using Guytp.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace Guytp.Networking
{
    public abstract class NetworkServer : IDisposable
    {
        private TcpListener _tcpListener;

        private Thread _thread;

        private bool _isAlive;

        private readonly IPAddress _ipAddress;

        private readonly int _port;

        private readonly X509Certificate _sslServerCertificate;

        protected abstract Dictionary<Type, MessageHandlerDelegate> MessageHandlers { get; }

        protected NetworkServer(IPAddress ipAddress, int port, X509Certificate sslServerCertificate = null)
        {
            _ipAddress = ipAddress;
            _port = port;
            _sslServerCertificate = sslServerCertificate;
        }

        public void Start()
        {
            if (_tcpListener != null)
                throw new Exception("Already started");
            _tcpListener = new TcpListener(_ipAddress, _port);
            _tcpListener.Start();
            _isAlive = true;
            _thread = new Thread(MainThread) { Name = GetType().Name + " Network Server" };
            _thread.Start();
            OnStart();
        }

        protected virtual void OnStart()
        {
        }

        public void Stop()
        {
            OnStop();
            _isAlive = false;
            _tcpListener?.Stop();
            _thread?.Join();
            _tcpListener = null;
            _thread = null;
        }

        protected virtual void OnStop()
        {

        }

        private void MainThread()
        {
            Logger.ApplicationInstance.Debug("Network server thread is alive");
            List<NetworkConnection> connections = new List<NetworkConnection>();
            while (_isAlive)
            {
                try
                {
                    // Accept new sockets
                    while (_tcpListener.Pending())
                    {
                        try
                        {
                            connections.Add(_sslServerCertificate != null ? new NetworkConnection(_tcpListener.AcceptTcpClient(), _sslServerCertificate) : new NetworkConnection(_tcpListener.AcceptTcpClient()));
                            Logger.ApplicationInstance.Debug("New connection accepted");
                        }
                        catch (Exception ex)
                        {
                            Logger.ApplicationInstance.Error("Failed to accept socket", ex);
                        }
                    }

                    // Check for any messages - although we only read one per connection per loop to ensure no connection can hold upp the entire queue
                    foreach (NetworkConnection connection in connections)
                    {
                        // Read a message from this connection
                        try
                        {
                            connection.PerformReadCycle();
                        }
                        catch (Exception ex)
                        {
                            connection.Invalidate("Error reading data from connection");
                            Logger.ApplicationInstance.Error("Error reading data from connection", ex);
                            continue;
                        }

                        // Now try to process any messages held by this connection
                        try
                        {
                            object queuedMessage = connection.DequeueReceivedMessage();
                            if (queuedMessage != null)
                            {
                                Type messageType = queuedMessage.GetType();
                                if (MessageHandlers.ContainsKey(messageType) && CanHandleMessage(messageType, connection.OutboundMessageQueue))
                                {
                                    Action act = () =>
                                    {
                                        try
                                        {
                                            MessageHandlers[messageType](queuedMessage, connection.OutboundMessageQueue);
                                        }
                                        catch (Exception ex)
                                        {
                                            Logger.ApplicationInstance.Error("Error executing message handler of type " + messageType.Name, ex);
                                        }
                                    };
                                    act.BeginInvoke(null, null);
                                }
                                else
                                    throw new Exception("Unknown message " + messageType.Name);
                            }
                        }
                        catch (Exception ex)
                        {
                            connection.Invalidate("Exception handling/dequeueing message");
                            Logger.ApplicationInstance.Error("Error handling/dequeueing message read", ex);
                            continue;
                        }
                    }

                    // Send any data that is queued to each connection and performs a ping-check
                    foreach (NetworkConnection connection in connections)
                    {
                        try
                        {
                            connection.PerformPingCheck();
                        }
                        catch (Exception ex)
                        {
                            connection.Invalidate("Exception attempting to ping check client");
                            Logger.ApplicationInstance.Error("Error with ping check", ex);
                            continue;
                        }
                        try
                        {
                            connection.PerformWriteCycle();
                        }
                        catch (Exception ex)
                        {
                            connection.Invalidate("Exception attempting to write to client");
                            Logger.ApplicationInstance.Error("Error performing write cycle to client", ex);
                            continue;
                        }
                    }

                    // Kill any bad connections
                    NetworkConnection[] invalidatedConnections = connections.Where(cl => cl.IsInvalidated).ToArray();
                    foreach (NetworkConnection connection in invalidatedConnections)
                        try
                        {
                            Logger.ApplicationInstance.Debug("Disconnecting client that was invalidated due to " + connection.InvalidationReason);
                            connection.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Logger.ApplicationInstance.Error("Error when removing invalidated client", ex);
                        }
                        finally
                        {
                            connections.Remove(connection);
                        }

                }
                catch (Exception ex)
                {
                    Logger.ApplicationInstance.Error("Fatal error processing network server", ex);
                }
                Thread.Sleep(50);
            }

            // Now disconnect everyone
            Logger.ApplicationInstance.Info("Network server thread terminated, disconnecting clients");
            foreach (NetworkConnection client in connections)
                try
                {
                    client.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.ApplicationInstance.Error("Failed to dispose client", ex);
                }
            Logger.ApplicationInstance.Info("Finished network server thread");
        }

        protected virtual bool CanHandleMessage(Type messageType, NetworkMessageQueue outboundMessageQueue)
        {
            return true;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
