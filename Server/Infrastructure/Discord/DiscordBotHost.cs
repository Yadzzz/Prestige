using System.Linq;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.SlashCommands;
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
        private readonly UsersService _usersService;

        public DiscordBotHost(DiscordOptions options, UsersService usersService)
        {
            _options = options;
            _client = DiscordClientFactory.Create(_options);
            _usersService = usersService;

            WireEvents();
            ConfigureSlashCommands();
            ConfigureCommands();
        }

        public DiscordClient Client => _client;

        private void WireEvents()
        {
            _client.ClientErrored += async (s, e) =>
            {
                Console.WriteLine($"[Discord Client Error] {e.Exception.GetType().Name}: {e.Exception.Message}");
                await Task.CompletedTask;
            };

            _client.SocketErrored += async (s, e) =>
            {
                Console.WriteLine($"[Discord Socket Error] {e.Exception?.Message}");
                await Task.CompletedTask;
            };

            _client.ComponentInteractionCreated += ButtonHandler.HandleButtons;
            _client.ModalSubmitted += RaceInteractionHandler.HandleModal;

            _client.Ready += async (s, e) =>
            {
                Console.WriteLine("Bot is ready ->");
                await Task.CompletedTask;
            };

            _client.GuildMemberAdded += async (s, e) =>
            {
                try
                {
                    await _usersService.EnsureUserAsync(e.Member.Id.ToString(), e.Member.Username, e.Member.DisplayName);
                }
                catch
                {
                    // Ignore failures here; user will still be created on first command.
                }
            };
        }

        private void ConfigureSlashCommands()
        {
            var slash = _client.UseSlashCommands();

            if (_options.GuildId != 0)
            {
                slash.RegisterCommands<PingSlashCommand>(_options.GuildId);
            }
            else
            {
                slash.RegisterCommands<PingSlashCommand>();
            }
        }

        private void ConfigureCommands()
        {
            var commands = _client.UseCommandsNext(new CommandsNextConfiguration
            {
                StringPrefixes = new[] { "!" },   // PREFIX is "!"
                EnableMentionPrefix = false
            });

            commands.CommandErrored += async (s, e) =>
            {
                if (e.Exception is CommandNotFoundException)
                {
                    var env = ServerEnvironment.GetServerEnvironment();
                    var aiService = env.ServerManager.AiCommandResolverService;

                    if (aiService == null) return;

                    var messageContent = e.Context.Message.Content;
                    var validCommands = s.RegisteredCommands.Keys.Select(k => "!" + k).Distinct();

                    var result = await aiService.ResolveAsync(messageContent, validCommands);

                    // Validate that the AI actually returned a known command
                    var isRealCommand = result != null 
                                        && result.IsMatch 
                                        && (validCommands.Contains(result.Command, StringComparer.OrdinalIgnoreCase) 
                                            || validCommands.Contains("!" + result.Command, StringComparer.OrdinalIgnoreCase));

                    if (isRealCommand && result != null)
                    {
                        // Ensure we display the command with prefix if the AI stripped it
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
                            .WithFooter("🤖 Prestige AI")
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
                            .WithFooter("🤖 Prestige AI")
                            .WithTimestamp(DateTimeOffset.UtcNow);

                        await e.Context.RespondAsync(embed: embed);
                    }
                    // If no match and no suggestion, do nothing (silent fail) or generic message
                }
            };

            // Register your module
            commands.RegisterCommands<BalanceCommand>();
            commands.RegisterCommands<DepositCommand>();
            commands.RegisterCommands<WithdrawCommand>();
            commands.RegisterCommands<StakeCommand>();
            commands.RegisterCommands<AdminBalanceCommand>();
            commands.RegisterCommands<CoinflipCommand>();
            commands.RegisterCommands<RaceCommand>();
            commands.RegisterCommands<CancelCommand>();
        }

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
