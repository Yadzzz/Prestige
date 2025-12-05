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

        private void WireEvents()
        {
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
                //slash.RegisterCommands<SlashTest>(_options.GuildId);
                //slash.RegisterCommands<SlashEmbed>(_options.GuildId);
            }
            else
            {
                //slash.RegisterCommands<SlashTest>();
                //slash.RegisterCommands<SlashEmbed>();
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
        }

        public async Task StartAsync()
        {
            await _client.ConnectAsync();
        }
    }
}
