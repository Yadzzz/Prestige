namespace Server
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.White;

            ServerEnvironment.GetServerEnvironment().Initialize();

            while (true)
            {
                Console.ReadKey();
            }
        }
    }
}
