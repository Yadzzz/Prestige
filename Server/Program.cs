using Server.Infrastructure;

namespace Server
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.White;

            var env = ServerEnvironment.GetServerEnvironment();
            env.Initialize();

            await env.ServerManager.DiscordBotHost.StartAsync();

            await Task.Delay(-1); // keep app alive
        }
    }
}
