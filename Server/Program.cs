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

            // Handle graceful shutdown
            var tcs = new TaskCompletionSource();
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                Console.WriteLine("Shutting down...");
                env.ServerManager.StopAsync().Wait();
                tcs.TrySetResult();
            };
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                Console.WriteLine("Shutting down...");
                env.ServerManager.StopAsync().Wait();
                tcs.TrySetResult();
            };

            await tcs.Task;
        }
    }
}
