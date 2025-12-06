using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.SlashCommands;
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

            // Register your module
            commands.RegisterCommands<BalanceCommand>();
            commands.RegisterCommands<DepositCommand>();
            commands.RegisterCommands<WithdrawCommand>();
            commands.RegisterCommands<StakeCommand>();
            commands.RegisterCommands<AdminBalanceCommand>();
            commands.RegisterCommands<CoinflipCommand>();
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
