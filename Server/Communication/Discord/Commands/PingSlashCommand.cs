using System.Threading.Tasks;
using DSharpPlus.Entities;
using DSharpPlus.Commands;
using System.ComponentModel;

namespace Server.Communication.Discord.Commands
{
    public class PingSlashCommand
    {
        [Command("ping")]
        [Description("Simple health check command.")]
        public async Task PingAsync(CommandContext ctx)
        {
            var embed = new DiscordEmbedBuilder()
                .WithTitle("Pong")
                .WithDescription($"{ServerConfiguration.ShortName} bot is online and responding.")
                .WithColor(DiscordColor.Blurple);

            await ctx.RespondAsync(embed.Build());
        }
    }
}
