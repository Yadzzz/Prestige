using System.Threading.Tasks;
using System.Linq;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Server;
using Server.Infrastructure.Discord;

namespace Server.Communication.Discord.Commands
{
    public class RaceCommand : ModuleBase<SocketCommandContext>
    {
        [Command("racecreate")]
        //[RequireRoles(RoleCheckMode.Any, DiscordIds.StaffRoleId)]
        public async Task RaceCreate()
        {
            var user = Context.User as SocketGuildUser;
            if (user == null || !user.IsStaff())
            {
                await ReplyAsync("You do not have permission to execute this command.");
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle("Race Configuration")
                .WithDescription("Configure the new race using the menu below.")
                .WithColor(Color.Blue);

            var menuBuilder = new SelectMenuBuilder()
                .WithCustomId("race_config_menu")
                .WithPlaceholder("Select an option...")
                .AddOption("Set Duration", "race_duration", "Set the duration of the race")
                .AddOption("Set Winners", "race_winners", "Set the number of winners")
                .AddOption("Set Prize Pool", "race_prizes", "Configure prizes")
                .AddOption("Start Race", "race_start", "Launch the race!");

            var component = new ComponentBuilder()
                .WithSelectMenu(menuBuilder)
                .Build();

            await ReplyAsync(embed: embed.Build(), components: component);
        }

        [Command("raceend")]
        //[RequireRoles(RoleCheckMode.Any, DiscordIds.StaffRoleId)]
        public async Task RaceEnd()
        {
            var user = Context.User as SocketGuildUser;
            if (user == null || !user.IsStaff())
            {
                await ReplyAsync("You do not have permission to execute this command.");
                return;
            }

            var env = ServerEnvironment.GetServerEnvironment();
            await env.ServerManager.RaceService.EndRaceAsync();
            await ReplyAsync("Race ended manually.");
        }
    }
}
