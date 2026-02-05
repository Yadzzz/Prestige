using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.EventArgs;
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
        private readonly DiscordClient _client;
        private readonly ServerManager _serverManager;

        public DiscordBotHost(DiscordOptions options, ServerManager serverManager)
        {
            _options = options;
            _serverManager = serverManager;

            var builder = DiscordClientFactory.CreateBuilder(_options);

            // Configure Services
            builder.ConfigureServices(services =>
            {
                services.AddLogging(logging => 
                {
                    logging.AddConsole();
                    logging.SetMinimumLevel(_options.MinimumLogLevel);
                });
                
                // Register existing services from ServerManager
                /*services.AddSingleton(_serverManager);
                services.AddSingleton(_serverManager.UsersService);
                services.AddSingleton(_serverManager.TransactionsService);
                services.AddSingleton(_serverManager.BalanceAdjustmentsService);
                services.AddSingleton(_serverManager.StakesService);
                services.AddSingleton(_serverManager.CoinflipsService);
                services.AddSingleton(_serverManager.BlackjackService);
                services.AddSingleton(_serverManager.LogsService);
                services.AddSingleton(_serverManager.LiveFeedService);
                services.AddSingleton(_serverManager.RaceService);
                services.AddSingleton(_serverManager.AiCommandResolverService);*/
            });

            // Configure Event Handlers
            builder.ConfigureEventHandlers(b =>
            {
                b.HandleComponentInteractionCreated(ButtonHandler.HandleButtons);
                b.HandleModalSubmitted(ModalHandler.HandleModals);

                b.HandleSessionCreated(async (s, e) =>
                {
                    Console.WriteLine("Bot is ready ->");
                    _serverManager.LoggerManager.Log("Bot is ready");
                    await Task.CompletedTask;
                });

                b.HandleGuildMemberAdded(async (s, e) =>
                {
                    try
                    {
                        await _serverManager.UsersService.EnsureUserAsync(e.Member.Id.ToString(), e.Member.Username, e.Member.DisplayName);
                    }
                    catch
                    {
                        // Ignore failures here; user will still be created on first command.
                    }
                });
            });

            // Configure New Commands (Replacement for SlashCommands)
            builder.UseCommands((serviceProvider, extension) =>
            {
                extension.AddCommands([typeof(PingSlashCommand), typeof(BroadcastSlashCommand)]);
            }, new CommandsConfiguration
            {
                DebugGuildId = _options.GuildId != 0 ? _options.GuildId : 0
            });

            // Configure Legacy CommandsNext
            builder.UseCommandsNext(commands =>
            {
                commands.CommandExecuted += async (s, e) =>
                {
                    var msg = $"[CMD EXECUTED] User: {e.Context.User.Username}#{e.Context.User.Discriminator} " +
                        $"({e.Context.User.Id}) | Command: !{e.Command.Name} | Message: {e.Context.RawArgumentString}";
                    
                    Console.WriteLine(msg);
                    _serverManager.LoggerManager.Log(msg);
                    await Task.CompletedTask;
                };

                commands.CommandErrored += async (s, e) =>
                {
                    var msg = $"[CMD ERROR] User: {e.Context.User.Username}#{e.Context.User.Discriminator} " +
                        $"({e.Context.User.Id}) | Command: !{e.Command?.Name ?? "UNKNOWN"} | Error: {e.Exception.Message}";
                    
                    Console.WriteLine(msg);
                    _serverManager.LoggerManager.LogError(msg);

                    if (e.Exception is ChecksFailedException)
                    {
                        var embed = new DSharpPlus.Entities.DiscordEmbedBuilder()
                            .WithTitle("⛔ Access Denied")
                            .WithDescription("You do not have permission to execute this command.")
                            .WithColor(DSharpPlus.Entities.DiscordColor.Red)
                            .WithTimestamp(DateTimeOffset.UtcNow);

                        await e.Context.RespondAsync(embed: embed);
                        return;
                    }

                    if (e.Exception is CommandNotFoundException)
                    {
                        var aiService = _serverManager.AiCommandResolverService;

                        if (aiService == null) return;

                        var messageContent = e.Context.Message.Content;
                        var validCommands = s.RegisteredCommands.Keys.Select(k => "!" + k).Distinct();

                        var result = await aiService.ResolveAsync(messageContent, validCommands);

                        var isRealCommand = result != null 
                                            && result.IsMatch 
                                            && (validCommands.Contains(result.Command, StringComparer.OrdinalIgnoreCase) 
                                                || validCommands.Contains("!" + result.Command, StringComparer.OrdinalIgnoreCase));

                        if (isRealCommand && result != null)
                        {
                            var cmd = result.Command ?? "";
                            var commandDisplay = cmd.StartsWith("!") ? cmd : "!" + cmd;

                            var argsDisplay = string.IsNullOrWhiteSpace(result.Args) || result.Args.Trim().ToLower() == "null" 
                                ? string.Empty 
                                : result.Args;

                            var description = string.IsNullOrEmpty(argsDisplay)
                                ? $"Did you mean **{commandDisplay}**?"
                                : $"Did you mean **{commandDisplay}** {argsDisplay}?";

                            var embed = new DSharpPlus.Entities.DiscordEmbedBuilder()
                                .WithTitle("🤖 Command Not Found")
                                .WithDescription(description)
                                .WithColor(DSharpPlus.Entities.DiscordColor.Blurple)
                                .WithThumbnail("https://i.imgur.com/PspKnEB.gif")
                                .WithFooter($"🤖 {ServerConfiguration.ShortName} AI")
                                .WithTimestamp(DateTimeOffset.UtcNow);

                            await e.Context.RespondAsync(embed: embed);
                        }
                        else
                        {
                            var embed = new DSharpPlus.Entities.DiscordEmbedBuilder()
                                .WithTitle("❌ Command Not Found")
                                .WithDescription("I couldn't recognize that command. Please check for typos.")
                                .WithColor(DSharpPlus.Entities.DiscordColor.Red)
                                .WithThumbnail("https://i.imgur.com/PspKnEB.gif")
                                .WithFooter($"🤖 {ServerConfiguration.ShortName} AI")
                                .WithTimestamp(DateTimeOffset.UtcNow);

                            await e.Context.RespondAsync(embed: embed);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[Command Error] {e.Exception}");
                        var embed = new DSharpPlus.Entities.DiscordEmbedBuilder()
                            .WithTitle("⚠️ Error")
                            .WithDescription($"An error occurred: {e.Exception.Message}")
                            .WithColor(DSharpPlus.Entities.DiscordColor.Orange)
                            .WithTimestamp(DateTimeOffset.UtcNow);

                        await e.Context.RespondAsync(embed: embed);
                    }
                };

                commands.RegisterCommands<BalanceCommand>();
                commands.RegisterCommands<DepositCommand>();
                commands.RegisterCommands<WithdrawCommand>();
                commands.RegisterCommands<StakeCommand>();
                commands.RegisterCommands<AdminBalanceCommand>();
                commands.RegisterCommands<CoinflipCommand>();
                commands.RegisterCommands<BlackjackCommand>();
                commands.RegisterCommands<HigherLowerCommand>();
                commands.RegisterCommands<MinesCommand>();
                commands.RegisterCommands<RaceCommand>();
                commands.RegisterCommands<BuyCommand>();
                commands.RegisterCommands<CancelCommand>();
                commands.RegisterCommands<ChestCommand>();
                commands.RegisterCommands<ReferralCommands>();
                commands.RegisterCommands<WagerLockCommand>();
                commands.RegisterCommands<HelpCommand>();
                commands.RegisterCommands<VaultCommand>();

            }, new CommandsNextConfiguration
            {
                StringPrefixes = new[] { "!" },
                EnableMentionPrefix = false,
                EnableDefaultHelp = false
            });

            _client = builder.Build();
        }

        public DiscordClient Client => _client;

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _client.ConnectAsync();

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
    }
}
