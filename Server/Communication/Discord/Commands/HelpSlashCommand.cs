using System.Threading.Tasks;
using DSharpPlus.Commands;
using DSharpPlus.Entities;
using System.ComponentModel;
using Server.Client.Help;

namespace Server.Communication.Discord.Commands
{
    public class HelpSlashCommand
    {
        [Command("help")]
        [Description("Displays the list of available commands.")]
        public async Task HelpSlash(CommandContext ctx)
        {
            var member = ctx.Member;
            if (member == null) return;

            var embed = HelpService.BuildHelpEmbed(member);
            await ctx.RespondAsync(embed.Build());
        }

        [Command("commands")]
        [Description("Displays the list of available commands.")]
        public async Task CommandsSlash(CommandContext ctx)
        {
            await HelpSlash(ctx);
        }
    }
}
