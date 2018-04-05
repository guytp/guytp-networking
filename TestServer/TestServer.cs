using Guytp.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using TestProtocol;

namespace TestServer
{
    public class TestServer : NetworkServer
    {
        protected override Dictionary<Type, MessageHandlerDelegate> MessageHandlers { get; }

        #region Constructors
        /// <summary>
        /// Create a new instance of this class.
        /// </summary>
        public TestServer()
            : base(IPAddress.Loopback, 7357, new X509Certificate2("../../../Ssl/TestServer.guytp.org.pfx", "TestPassword"))
        {
            MessageHandlers = new Dictionary<Type, MessageHandlerDelegate>
            {
                { typeof(TestRequest), (m, q) => q.SendMessage(new TestResponse(new string(((TestRequest)m).Message.Reverse().ToArray()))) }
            };
        }
        #endregion

    }
}