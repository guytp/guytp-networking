using System;

namespace Guytp.Networking
{
    public abstract class MessageHandler<T> : IMessageHandler
    {
        public void Handle(object message, NetworkMessageQueue queue)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            if (!(message is T))
                throw new ArgumentException("Expected message of type " + typeof(T).GetType().Name + " but got " + message.GetType().Name);
            Handle((T)message, queue);
        }

        public abstract void Handle(T message, NetworkMessageQueue queue);
    }
}