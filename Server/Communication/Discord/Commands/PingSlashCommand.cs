using System.Threading.Tasks;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;

namespace Server.Communication.Discord.Commands
{
    public class PingSlashCommand : ApplicationCommandModule
    {
        [SlashCommand("ping", "Simple health check command.")]
        public async Task PingAsync(InteractionContext ctx)
        {
            var embed = new DiscordEmbedBuilder()
                .WithTitle("Pong")
                .WithDescription($"{ServerConfiguration.ShortName} bot is online and responding.")
                .WithColor(DiscordColor.Blurple);

            await ctx.CreateResponseAsync(embed.Build());
        }
    }
}
