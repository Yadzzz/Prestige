using DSharpPlus;
using Microsoft.Extensions.Logging;

namespace Server.Infrastructure.Discord
{
    public class DiscordOptions
    {
        public string Token { get; set; } = ""; // TODO: move to config/env

        public TokenType TokenType { get; set; } = TokenType.Bot;
        public DiscordIntents Intents { get; set; } = DiscordIntents.AllUnprivileged;
        public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;

        public ulong GuildId { get; set; }
    }
}
