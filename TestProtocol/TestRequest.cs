using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProtocol
{
    [Serializable]
    public class TestRequest
    {
        public string Message { get; }

        public TestRequest(string message)
        {
            Message = message;
        }
    }
}
