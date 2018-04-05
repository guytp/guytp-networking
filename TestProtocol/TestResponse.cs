using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProtocol
{
    [Serializable]
    public class TestResponse
    {
        public string Message { get; }

        public TestResponse(string message)
        {
            Message = message;
        }
    }
}