using DSharpPlus;

namespace Server.Infrastructure.Discord
{
    public static class DiscordClientFactory
    {
        public static DiscordClientBuilder CreateBuilder(DiscordOptions options)
        {
            return DiscordClientBuilder.CreateDefault(options.Token, options.Intents)
                .SetLogLevel(options.MinimumLogLevel);
        }
    }
}
