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
                MinimumLogLevel = options.MinimumLogLevel,
                AutoReconnect = true,
                //ReconnectIndefinitely = true,
                GatewayCompressionLevel = GatewayCompressionLevel.Stream,
                LargeThreshold = 250
            };

            return new DiscordClient(config);
        }
    }
}
