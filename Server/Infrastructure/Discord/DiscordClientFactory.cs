using DSharpPlus;

namespace Server.Infrastructure.Discord
{
    public static class DiscordClientFactory
    {
        public static DiscordClient Create(DiscordOptions options)
        {
            var config = new DiscordConfiguration
            {
                Token = options.Token,
                TokenType = options.TokenType,
                Intents = options.Intents,
                MinimumLogLevel = options.MinimumLogLevel
            };

            return new DiscordClient(config);
        }
    }
}
