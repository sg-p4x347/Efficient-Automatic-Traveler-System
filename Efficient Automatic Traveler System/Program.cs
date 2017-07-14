using System;
using System.Runtime.ExceptionServices;

namespace Efficient_Automatic_Traveler_System
{

    class Program
    {
        [HandleProcessCorruptedStateExceptions]
        static void Main()
        {
            try
            {
                server = new Server();
                server.Start();
            } catch (AccessViolationException ex)
            {
                Console.WriteLine("Caught ODBC exception in Main()");
                Console.WriteLine(ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        public static Server server;
    }
}
