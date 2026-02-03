using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using Server.Client.Help;

namespace Server.Communication.Discord.Commands
{
    public class HelpCommand : BaseCommandModule
    {
        [Command("help")]
        [Aliases("commands")]
        [Description("Displays the list of available commands.")]
        public async Task Help(CommandContext ctx)
        {
            var embed = HelpService.BuildHelpEmbed(ctx.Member);
            await ctx.RespondAsync(embed);
        }
    }
}
