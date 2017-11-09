using System;

namespace Guytp.Networking
{
    public class ConnectionErrorEventArgs : EventArgs
    {
        public Exception Error { get; private set; }

        public ConnectionErrorEventArgs(Exception error)
        {
            Error = error;
        }
    }
}