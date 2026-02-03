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

            // Start Discord Bot in background
            var botTask = env.ServerManager.DiscordBotHost.StartAsync();

            Console.WriteLine("Bot is running...");

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

            // Wait for shutdown signal or bot crash
            await Task.WhenAny(botTask, tcs.Task);
        }
    }
}
