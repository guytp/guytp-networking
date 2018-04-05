using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestClient
{
    class Program
    {
        static void Main(string[] args)
        {
            using (TestClient testClient = new TestClient())
            {
                testClient.Start();
                while (Console.ReadKey().Key != ConsoleKey.Q)
                {
                }
                testClient.Stop();
            }
        }
    }
}
