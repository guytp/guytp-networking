using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestServer
{
    class Program
    {
        static void Main(string[] args)
        {
            using (TestServer server = new TestServer())
            {
                server.Start();
                while (Console.ReadKey().Key != ConsoleKey.Q)
                {
                }
                server.Stop();
            }
        }
    }
}
