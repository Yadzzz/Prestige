using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Server;
using Server.Communication.Discord.Commands;
using Server.Communication.Discord.Interactions;
using Server.Client.Users;

namespace Server.Infrastructure.Discord
{
    public class DiscordBotHost
    {
        private readonly DiscordOptions _options;
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly InteractionService _interactions;
        private readonly ServerManager _serverManager;
        private IServiceProvider _services;

        public DiscordBotHost(DiscordOptions options, ServerManager serverManager)
        {
            _options = options;
            _serverManager = serverManager;

            var config = new DiscordSocketConfig
            {
                GatewayIntents = _options.Intents,
                LogLevel = LogSeverity.Info,
                MessageCacheSize = 1000,
                AlwaysDownloadUsers = true
            };

            _client = new DiscordSocketClient(config);

            _commands = new CommandService(new CommandServiceConfig
            {
                LogLevel = LogSeverity.Info,
                CaseSensitiveCommands = false,
                DefaultRunMode = global::Discord.Commands.RunMode.Async
            });

            _interactions = new InteractionService(_client.Rest, new InteractionServiceConfig
            {
                LogLevel = LogSeverity.Info,
                DefaultRunMode = global::Discord.Interactions.RunMode.Async
            });

            // Configure Event Handlers
            _client.Log += LogAsync;
            _commands.Log += LogAsync;
            _interactions.Log += LogAsync;

            _client.Ready += ReadyAsync;
            _client.MessageReceived += HandleMessageAsync;
            _client.InteractionCreated += HandleInteractionAsync;
            _client.UserJoined += HandleUserJoinedAsync;
        }

        private IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection()
                .AddSingleton(_options)
                .AddSingleton(_serverManager)
                .AddSingleton(_client)
                .AddSingleton(_commands)
                .AddSingleton(_interactions)
                .AddSingleton(_serverManager.UsersService)
                .AddSingleton(_serverManager.TransactionsService)
                .AddSingleton(_serverManager.BalanceAdjustmentsService)
                .AddSingleton(_serverManager.StakesService)
                .AddSingleton(_serverManager.CoinflipsService)
                .AddSingleton(_serverManager.BlackjackService)
                .AddSingleton(_serverManager.LogsService)
                .AddSingleton(_serverManager.LiveFeedService)
                .AddSingleton(_serverManager.RaceService)
                .AddSingleton(_serverManager.AiCommandResolverService);

            return services.BuildServiceProvider();
        }

        public DiscordSocketClient Client => _client;

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            // Configure Services (delayed until Start to ensure ServerManager is fully initialized)
            _services = ConfigureServices();

            // Register modules
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
            await _interactions.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _client.LoginAsync(TokenType.Bot, _options.Token);
                    await _client.StartAsync();

                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Discord Host] Fatal error, restarting client: {ex.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                }
            }
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }

        private async Task ReadyAsync()
        {
            Console.WriteLine("Bot is ready ->");
            _serverManager.LoggerManager.Log("Bot is ready");

            try 
            {
                if (_options.GuildId != 0)
                {
                    await _interactions.RegisterCommandsToGuildAsync(_options.GuildId);
                }
                else
                {
                    await _interactions.RegisterCommandsGloballyAsync();
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Error registering interactions: {ex.Message}");
            }
        }

        private async Task HandleUserJoinedAsync(SocketGuildUser user)
        {
            try
            {
                await _serverManager.UsersService.EnsureUserAsync(user.Id.ToString(), user.Username, user.DisplayName);
            }
            catch
            {
                // Ignore failures here; user will still be created on first command.
            }
        }

        private async Task HandleInteractionAsync(SocketInteraction interaction)
        {
            try
            {
                if (interaction is SocketMessageComponent component)
                {
                    // Dispatch to manual button handlers
                    await ButtonHandler.HandleButtons(_client, component);
                    return;
                }

                if (interaction is SocketModal modal)
                {
                    await RaceInteractionHandler.HandleModal(_client, modal);
                    return;
                }

                var context = new SocketInteractionContext(_client, interaction);
                await _interactions.ExecuteCommandAsync(context, _services);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling interaction: {ex}");
            }
        }

        private async Task HandleMessageAsync(SocketMessage message)
        {
            if (message is not SocketUserMessage userMessage) return;
            if (userMessage.Author.IsBot) return;

            int argPos = 0;
            if (!(userMessage.HasCharPrefix('!', ref argPos) || 
                userMessage.HasMentionPrefix(_client.CurrentUser, ref argPos)))
                return;

            var context = new SocketCommandContext(_client, userMessage);

            var result = await _commands.ExecuteAsync(context, argPos, _services);

            if (!result.IsSuccess)
            {
                if (result.Error == CommandError.UnknownCommand)
                {
                    var aiService = _serverManager.AiCommandResolverService;

                    if (aiService == null) return;

                    var messageContent = context.Message.Content;
                    var validCommands = _commands.Commands.Select(k => "!" + k.Name).Distinct();

                    var aiResult = await aiService.ResolveAsync(messageContent, validCommands);

                    var isRealCommand = aiResult != null 
                                        && aiResult.IsMatch 
                                        && (validCommands.Contains(aiResult.Command, StringComparer.OrdinalIgnoreCase) 
                                            || validCommands.Contains("!" + aiResult.Command, StringComparer.OrdinalIgnoreCase));

                    if (isRealCommand && aiResult != null)
                    {
                        var cmd = aiResult.Command ?? "";
                        var commandDisplay = cmd.StartsWith("!") ? cmd : "!" + cmd;

                        var argsDisplay = string.IsNullOrWhiteSpace(aiResult.Args) || aiResult.Args.Trim().ToLower() == "null" 
                            ? string.Empty 
                            : aiResult.Args;

                        var description = string.IsNullOrEmpty(argsDisplay)
                            ? $"Did you mean **{commandDisplay}**?"
                            : $"Did you mean **{commandDisplay}** {argsDisplay}?";

                        var embed = new EmbedBuilder()
                            .WithTitle("🤖 Command Not Found")
                            .WithDescription(description)
                            .WithColor(Color.Blue)
                            .WithThumbnailUrl("https://i.imgur.com/PspKnEB.gif")
                            .WithFooter($"🤖 {ServerConfiguration.ShortName} AI")
                            .WithCurrentTimestamp();

                        await context.Channel.SendMessageAsync(embed: embed.Build());
                    }
                    else
                    {
                        var embed = new EmbedBuilder()
                            .WithTitle("❌ Command Not Found")
                            .WithDescription("I couldn't recognize that command. Please check for typos.")
                            .WithColor(Color.Red)
                            .WithThumbnailUrl("https://i.imgur.com/PspKnEB.gif")
                            .WithFooter($"🤖 {ServerConfiguration.ShortName} AI")
                            .WithCurrentTimestamp();

                        await context.Channel.SendMessageAsync(embed: embed.Build());
                    }
                }
                else if (result.Error == CommandError.UnmetPrecondition)
                {
                     var embed = new EmbedBuilder()
                        .WithTitle("⛔ Access Denied")
                        .WithDescription("You do not have permission to execute this command.")
                        .WithColor(Color.Red)
                        .WithCurrentTimestamp();

                    await context.Channel.SendMessageAsync(embed: embed.Build());
                }
                else
                {
                    Console.WriteLine($"[Command Error] {result.ErrorReason}");
                    var embed = new EmbedBuilder()
                        .WithTitle("⚠️ Error")
                        .WithDescription($"An error occurred: {result.ErrorReason}")
                        .WithColor(Color.Orange)
                        .WithCurrentTimestamp();

                    await context.Channel.SendMessageAsync(embed: embed.Build());
                }
            }
            else
            {
                 var msg = $"[CMD EXECUTED] User: {context.User.Username}#{context.User.Discriminator} " +
                        $"({context.User.Id}) | Command: {context.Message.Content}";
                    
                 Console.WriteLine(msg);
                 _serverManager.LoggerManager.Log(msg);
            }
        }
    }
}
