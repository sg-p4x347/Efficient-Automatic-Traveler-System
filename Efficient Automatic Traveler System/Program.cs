using System;

namespace Efficient_Automatic_Traveler_System
{

    class Program
    {
        static void Main()
        {
            try
            {
                server = new Server();
                server.Start();
            } catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        public static Server server;
    }
}
