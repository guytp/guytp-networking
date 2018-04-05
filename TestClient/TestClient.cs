using Guytp.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using TestProtocol;

namespace TestClient
{
    public class TestClient : NetworkClient
    {
        private readonly Timer _timer = new Timer();

        private int _reqNo = 1;

        private int _inCount = 0;

        private int _outCount = 0;

        #region Properties
        /// <summary>
        /// Gets the message handlers used by this network client.
        /// </summary>
        protected override Dictionary<Type, MessageHandlerDelegate> MessageHandlers { get; }
        #endregion

        #region Constructors
        /// <summary>
        /// Create a new instance of this class.
        /// </summary>
        /// <param name="hostname">
        /// The hostname to connect to.
        /// </param>
        /// <param name="port">
        /// The port to connect to.
        /// </param>
        public TestClient(string hostname = "127.0.0.1", int port = 7357)
            : base(System.Net.IPAddress.Parse(hostname), port, "TestServer.guytp.org", false)
        {
            // Setup message handlers
            MessageHandlers = new Dictionary<Type, MessageHandlerDelegate>
            {
                { typeof(TestResponse), (m, q) =>
                    {
                        _inCount++;
                        //if (_inCount % 1000 == 0)
                            Console.WriteLine("Inc:  " + _inCount);
                        Console.WriteLine("In:  " + ((TestResponse)m).Message);
                    }
                }
            };
            _timer.Elapsed += OnTimer;
            _timer.Interval = 1000;
            _timer.Enabled = true;
            _timer.AutoReset = true;
        }
        #endregion

        private void OnTimer(object sender, ElapsedEventArgs e)
        {
            if (IsConnected)
                Test("Req " + _reqNo++);
        }

        /// <summary>
        /// Search for chart bar data based on specified criteria.
        /// </summary>
        /// <param name="searchItems">
        /// The items to search for.
        /// </param>
        /// <returns>
        /// The ID of the search request which will be used in any response calls.
        /// </returns>
        public void Test(string message)
        {
            EnsureConnection();
            Console.WriteLine("Out: " + message);
            _outCount++;
            //if (_outCount % 1000 == 0)
                Console.WriteLine("Outc: " + _outCount);
            MessageQueue.SendMessage(new TestRequest(message));
        }

        /// <summary>
        /// Ensure we are connected and in a valid state to send messages, otherwise throw an exception.
        /// </summary>
        private void EnsureConnection()
        {
            if (!IsConnected)
                throw new Exception("Not connected");
            if (MessageQueue == null)
                throw new Exception("No message queue available");
        }
    }
}
