using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Guytp.Networking
{
    public class ConnectionInvalidatedEventArgs : EventArgs
    {
        public string Reason { get; private set; }

        public ConnectionInvalidatedEventArgs(string reason)
        {
            Reason = reason;
        }
    }
}