using DSharpPlus;
using DSharpPlus.SlashCommands;

namespace Server.Infrastructure.Discord
{
    public class DiscordBotHost
    {
        private readonly DiscordOptions _options;
        private readonly DiscordClient _client;

        public DiscordBotHost(DiscordOptions options)
        {
            _options = options;
            _client = DiscordClientFactory.Create(_options);

            WireEvents();
            ConfigureSlashCommands();
        }

        private void WireEvents()
        {
            _client.ComponentInteractionCreated += ButtonHandler.HandleButtons;

            _client.Ready += async (s, e) =>
            {
                Console.WriteLine("Bot is ready ✅");
                await Task.CompletedTask;
            };
        }

        private void ConfigureSlashCommands()
        {
            var slash = _client.UseSlashCommands();

            if (_options.GuildId != 0)
            {
                slash.RegisterCommands<SlashTest>(_options.GuildId);
                slash.RegisterCommands<SlashEmbed>(_options.GuildId);
            }
            else
            {
                slash.RegisterCommands<SlashTest>();
                slash.RegisterCommands<SlashEmbed>();
            }
        }

        public async Task StartAsync()
        {
            await _client.ConnectAsync();
        }
    }
}
