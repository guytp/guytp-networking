﻿using Guytp.Logging;
using System;
using System.Collections.Generic;

namespace Guytp.Networking
{
    public class NetworkMessageQueue
    {
        private readonly object _messageLocker = new object();
        private readonly Queue<object> _messages = new Queue<object>();

        public int QueueCount
        {
            get
            {
                lock (_messageLocker)
                    return _messages.Count;
            }
        }

        public void SendMessage<T>(T message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            lock (_messageLocker)
            {
                _messages.Enqueue(message);
                Logger.ApplicationInstance.Debug("Added " + message.GetType().Name + " to network message queue, size is now " + _messages.Count);
            }
        }

        internal object DequeueMessage()
        {
            if (_messages.Count < 1)
                return null;
            lock (_messageLocker)
            {
                object obj = _messages.Dequeue();
                Logger.ApplicationInstance.Debug("Dequeued " + obj.GetType().Name + " from network message queue, size is now " + _messages.Count);
                return obj;
            }
        }
    }
}