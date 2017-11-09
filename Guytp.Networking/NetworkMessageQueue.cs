using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                _messages.Enqueue(message);
        }

        internal object DequeueMessage()
        {
            if (_messages.Count < 1)
                return null;
            lock (_messageLocker)
                return _messages.Dequeue();
        }
    }
}