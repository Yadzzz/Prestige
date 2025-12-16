using Server.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

            // Start ASP.NET Core Web Host
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers();
            builder.Services.AddSingleton(env.ServerManager); // Inject ServerManager into Controllers

            // Configure Kestrel to listen on a specific port (e.g., 5000)
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(5000); // Listen on port 5000
            });

            var app = builder.Build();

            app.MapControllers();

            Console.WriteLine("Starting Web Server on port 5000...");
            var webTask = app.RunAsync();

            // Wait for both (or just wait indefinitely)
            await Task.WhenAny(botTask, webTask);

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
