using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Server;
using Server.Infrastructure.Discord;

namespace Server.Communication.Discord.Commands
{
    public class RaceCommand : BaseCommandModule
    {
        [Command("racecreate")]
        public async Task RaceCreate(CommandContext ctx)
        {
            if (ctx.Member == null || !ctx.Member.Roles.Any(r => r.Id == DiscordIds.StaffRoleId))
            {
                await ctx.RespondAsync("You are not authorized to use this command.");
                return;
            }

            var embed = new DiscordEmbedBuilder()
                .WithTitle("Race Configuration")
                .WithDescription("Configure the new race using the menu below.")
                .WithColor(DiscordColor.Blue);

            var options = new[]
            {
                new DiscordSelectComponentOption("Set Duration", "race_duration", "Set the duration of the race"),
                new DiscordSelectComponentOption("Set Winners", "race_winners", "Set the number of winners"),
                new DiscordSelectComponentOption("Set Prize Pool", "race_prizes", "Configure prizes"),
                new DiscordSelectComponentOption("Start Race", "race_start", "Launch the race!")
            };

            var dropdown = new DiscordSelectComponent("race_config_menu", "Select an option...", options);

            await ctx.RespondAsync(new DiscordMessageBuilder()
                .AddEmbed(embed)
                .AddComponents(dropdown));
        }

        [Command("raceend")]
        public async Task RaceEnd(CommandContext ctx)
        {
            if (ctx.Member == null || !ctx.Member.Roles.Any(r => r.Id == DiscordIds.StaffRoleId))
            {
                await ctx.RespondAsync("You are not authorized to use this command.");
                return;
            }

            var env = ServerEnvironment.GetServerEnvironment();
            env.ServerManager.RaceService.EndRace();
            await ctx.RespondAsync("Race ended manually.");
        }
    }
}
