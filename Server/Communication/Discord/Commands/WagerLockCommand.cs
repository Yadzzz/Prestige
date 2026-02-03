using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Server.Client.Utils;
using Server.Infrastructure;
using Server.Infrastructure.Discord;

namespace Server.Communication.Discord.Commands
{
    public class WagerLockCommand : BaseCommandModule
    {
        [Command("wagerlock")]
        [Aliases("wl")]
        [Description("Adds a wager lock to a user.")]
        public async Task WagerLock(CommandContext ctx, DiscordMember member, string amount)
        {
            if (!ctx.Member.IsStaff())
            {
                await ctx.RespondAsync("You are not authorized to use this command.");
                return;
            }

            if (!GpParser.TryParseAmountInK(amount, out var amountK, out var error))
            {
                await ctx.RespondAsync($"Invalid amount: {error}");
                return;
            }

            var env = ServerEnvironment.GetServerEnvironment();
            var usersService = env.ServerManager.UsersService;

            var user = await usersService.EnsureUserAsync(member.Id.ToString(), member.Username, member.DisplayName);
            if (user == null)
            {
                await ctx.RespondAsync("Failed to load user.");
                return;
            }

            if (await usersService.AddWagerLockAsync(user.Identifier, amountK))
            {
                await ctx.RespondAsync($"Successfully added wager lock of {GpFormatter.Format(amountK)} to {member.DisplayName} (ID: {member.Id}).");
                
                await env.ServerManager.LogsService.LogAsync(
                    source: nameof(WagerLockCommand),
                    level: "Info",
                    userIdentifier: user.Identifier,
                    action: "WagerLockAdded",
                    message: $"Admin {ctx.User.Id} added {amountK}K wager lock to {user.Identifier}",
                    exception: null);
            }
            else
            {
                await ctx.RespondAsync("Failed to add wager lock.");
            }
        }
    }
}
