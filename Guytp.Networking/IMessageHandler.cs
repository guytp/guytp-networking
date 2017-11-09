namespace Guytp.Networking
{
    public interface IMessageHandler
    {
        void Handle(object message, NetworkMessageQueue queue);
    }
}