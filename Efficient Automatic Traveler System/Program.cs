using System;

namespace Efficient_Automatic_Traveler_System
{

    class Program
    {
        static void Main()
        {
            try
            {
                Server server = new Server();
                server.Start();
            } catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
