using Discord;
using Microsoft.Extensions.Logging;

namespace Server.Infrastructure.Discord
{
    public class DiscordOptions
    {
        public string Token { get; set; } = ""; // TODO: move to config/env

        public TokenType TokenType { get; set; } = TokenType.Bot;
        public GatewayIntents Intents { get; set; } = GatewayIntents.All;
        public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;

        public ulong GuildId { get; set; }
    }
}
