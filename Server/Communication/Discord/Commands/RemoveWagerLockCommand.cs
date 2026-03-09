using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Server.Client.Utils;
using Server.Infrastructure;
using Server.Infrastructure.Discord;

namespace Server.Communication.Discord.Commands
{
    public class RemoveWagerLockCommand : BaseCommandModule
    {
        [Command("remwagerlock")]
        [Aliases("removewl", "remwl", "removewagerlock")]
        [Description("Removes a wager lock from a user.")]
        public async Task RemoveWagerLock(CommandContext ctx, string amount, DiscordMember member)
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

            if (await usersService.RemoveWagerLockAsync(user.Identifier, amountK))
            {
                await ctx.RespondAsync($"Successfully removed wager lock of {GpFormatter.Format(amountK)} from {member.DisplayName} (ID: {member.Id}).");
                
                await env.ServerManager.LogsService.LogAsync(
                    source: nameof(RemoveWagerLockCommand),
                    level: "Info",
                    userIdentifier: user.Identifier,
                    action: "WagerLockRemoved",
                    message: $"Admin {ctx.User.Id} removed {amountK}K wager lock from {user.Identifier}",
                    exception: null);
            }
            else
            {
                await ctx.RespondAsync("Failed to remove wager lock.");
            }
        }
        
        // Also support backwards order <user> <amount> just in case
        [Command("remwagerlock")]
        public async Task RemoveWagerLock(CommandContext ctx, DiscordMember member, string amount)
        {
            await RemoveWagerLock(ctx, amount, member);
        }
    }
}
