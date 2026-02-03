using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Trees.Metadata;
using DSharpPlus.Entities;
using System.ComponentModel;
using System.Threading.Tasks;

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
