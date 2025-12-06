using System;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Server.Client.Users;
using Server.Client.Utils;
using Server.Infrastructure;

namespace Server.Communication.Discord.Commands
{
    [RequireRoles(RoleCheckMode.Any, "Staff", "Admin", "Moderator")]
    public class AdminBalanceCommand : BaseCommandModule
    {
        [Command("add")]
        public async Task AddAsync(CommandContext ctx, string amount, DiscordMember member)
        {
            await HandleBalanceChangeAsync(ctx, amount, member, isGift: false);
        }

        [Command("gift")]
        public async Task GiftAsync(CommandContext ctx, string amount, DiscordMember member)
        {
            await HandleBalanceChangeAsync(ctx, amount, member, isGift: true);
        }

        [Command("remove")]
        public async Task RemoveAsync(CommandContext ctx, string amount, DiscordMember member)
        {
            await HandleBalanceRemovalAsync(ctx, amount, member);
        }

        private async Task HandleBalanceChangeAsync(CommandContext ctx, string amount, DiscordMember targetMember, bool isGift)
        {
            var env = ServerEnvironment.GetServerEnvironment();
            var usersService = env.ServerManager.UsersService;

            if (!GpParser.TryParseAmountInK(amount, out var amountK))
            {
                await ctx.RespondAsync("Invalid amount. Examples: `!add 10 @user`, `!add 0.5 @user`, `!add 1b @user`, `!add 1000m @user`.");
                return;
            }
            if (amountK <= 0)
            {
                await ctx.RespondAsync("Amount must be greater than zero.");
                return;
            }

            var targetUser = await usersService.EnsureUserAsync(targetMember.Id.ToString(), targetMember.Username, targetMember.DisplayName);
            if (targetUser == null)
            {
                await ctx.RespondAsync("Failed to resolve target user.");
                return;
            }

            var success = usersService.AddBalance(targetUser.Identifier, amountK);
            if (!success)
            {
                await ctx.RespondAsync("Failed to update user balance. Please try again later.");
                return;
            }

            usersService.TryGetUser(targetUser.Identifier, out var updatedUser);
            var prettyAmount = GpFormatter.Format(amountK);
            var newBalanceText = updatedUser != null ? GpFormatter.Format(updatedUser.Balance) : "unknown";

            var staffName = ctx.Member?.DisplayName ?? ctx.User.Username;

            var title = isGift ? "Gifted Balance" : "Added Balance";
            var embed = new DiscordEmbedBuilder()
                .WithTitle(title)
                .AddField("Amount", $"**{prettyAmount}**", true)
                .AddField("Member", targetMember.Username, true)
                .AddField("Staff", staffName, true)
                .WithColor(DiscordColor.Green)
                .WithThumbnail(isGift ? "https://i.imgur.com/vFstFPx.gif" : "https://i.imgur.com/0qEQpNC.gif")
                .WithFooter("Prestige Bets")
                .WithTimestamp(DateTimeOffset.UtcNow);

            await ctx.RespondAsync(new DiscordMessageBuilder()
                .WithContent(targetMember.Mention)
                .AddEmbed(embed));
        }

        private async Task HandleBalanceRemovalAsync(CommandContext ctx, string amount, DiscordMember targetMember)
        {
            var env = ServerEnvironment.GetServerEnvironment();
            var usersService = env.ServerManager.UsersService;

            if (!GpParser.TryParseAmountInK(amount, out var amountK))
            {
                await ctx.RespondAsync("Invalid amount. Examples: `!remove 10 @user`, `!remove 0.5 @user`, `!remove 1b @user`, `!remove 1000m @user`.");
                return;
            }
            if (amountK <= 0)
            {
                await ctx.RespondAsync("Amount must be greater than zero.");
                return;
            }

            var targetUser = await usersService.EnsureUserAsync(targetMember.Id.ToString(), targetMember.Username, targetMember.DisplayName);
            if (targetUser == null)
            {
                await ctx.RespondAsync("Failed to resolve target user.");
                return;
            }

            var success = usersService.RemoveBalance(targetUser.Identifier, amountK);
            if (!success)
            {
                await ctx.RespondAsync("Failed to update user balance. Please try again later.");
                return;
            }

            usersService.TryGetUser(targetUser.Identifier, out var updatedUser);
            var prettyAmount = GpFormatter.Format(amountK);
            var newBalanceText = updatedUser != null ? GpFormatter.Format(updatedUser.Balance) : "unknown";

            var embed = new DiscordEmbedBuilder()
                .WithTitle("Balance Adjusted")
                .WithDescription($"Your balance was decreased by **{prettyAmount}** by staff.")
                .AddField("Balance", newBalanceText, true)
                .WithColor(DiscordColor.Red)
                .WithTimestamp(DateTimeOffset.UtcNow);

            await ctx.RespondAsync(new DiscordMessageBuilder()
                .WithContent(targetMember.Mention)
                .AddEmbed(embed));
        }
    }
}
