using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Server.Client.Users;
using Server.Client.Utils;
using Server.Client.Transactions;
using Server.Infrastructure;
using Server.Infrastructure.Discord;

namespace Server.Communication.Discord.Commands
{
    public class AdminBalanceCommand : BaseCommandModule
    {
        [Command("add")]
        public async Task AddAsync(CommandContext ctx, string amount, DiscordMember member)
        {
            if (!ctx.Member.IsStaff())
            {
                await ctx.RespondAsync("You are not authorized to use this command.");
                return;
            }
            await HandleBalanceChangeAsync(ctx, amount, member, BalanceAdjustmentType.AdminAdd);
        }

        [Command("gift")]
        public async Task GiftAsync(CommandContext ctx, string amount, DiscordMember member)
        {
            if (!ctx.Member.IsStaff())
            {
                await ctx.RespondAsync("You are not authorized to use this command.");
                return;
            }
            await HandleBalanceChangeAsync(ctx, amount, member, BalanceAdjustmentType.AdminGift);
        }

        [Command("remove")]
        public async Task RemoveAsync(CommandContext ctx, string amount, DiscordMember member)
        {
            if (!ctx.Member.IsStaff())
            {
                await ctx.RespondAsync("You are not authorized to use this command.");
                return;
            }
            await HandleBalanceRemovalAsync(ctx, amount, member);
        }

        private async Task HandleBalanceChangeAsync(CommandContext ctx, string amount, DiscordMember targetMember, BalanceAdjustmentType adjustmentType)
        {
            var env = ServerEnvironment.GetServerEnvironment();
            var usersService = env.ServerManager.UsersService;
            var balanceAdjustmentsService = env.ServerManager.BalanceAdjustmentsService;

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

            var success = await usersService.AddBalanceAsync(targetUser.Identifier, amountK);
            if (!success)
            {
                await ctx.RespondAsync("Failed to update user balance. Please try again later.");
                return;
            }

            var staffIdentifier = ctx.Member?.Id.ToString() ?? ctx.User.Id.ToString();
            await balanceAdjustmentsService.RecordAdjustmentAsync(
                targetUser,
                staffIdentifier,
                adjustmentType,
                amountK,
                source: "AdminCommand");

            var updatedUser = await usersService.GetUserAsync(targetUser.Identifier);
            var prettyAmount = GpFormatter.Format(amountK);
            var newBalanceText = updatedUser != null ? GpFormatter.Format(updatedUser.Balance) : "unknown";

            var staffName = ctx.Member?.DisplayName ?? ctx.User.Username;

            var title = adjustmentType == BalanceAdjustmentType.AdminGift ? "Gifted" : "Added Balance";
            var embed = new DiscordEmbedBuilder()
                .WithTitle(title)
                .AddField("Amount", $"**{prettyAmount}**", true)
                .AddField("Member", targetMember.Username, true)
                .AddField("Staff", staffName, true)
                .WithColor(DiscordColor.Green)
                .WithThumbnail(adjustmentType == BalanceAdjustmentType.AdminGift ? "https://i.imgur.com/vFstFPx.gif" : "https://i.imgur.com/0qEQpNC.gif")
                .WithFooter(ServerConfiguration.ServerName)
                .WithTimestamp(DateTimeOffset.UtcNow);

            await ctx.RespondAsync(new DiscordMessageBuilder()
                .WithContent(targetMember.Mention)
                .AddEmbed(embed));
        }

        private async Task HandleBalanceRemovalAsync(CommandContext ctx, string amount, DiscordMember targetMember)
        {
            var env = ServerEnvironment.GetServerEnvironment();
            var usersService = env.ServerManager.UsersService;
            var balanceAdjustmentsService = env.ServerManager.BalanceAdjustmentsService;

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

            var success = await usersService.RemoveBalanceAsync(targetUser.Identifier, amountK);
            if (!success)
            {
                await ctx.RespondAsync("Failed to update user balance. Please try again later.");
                return;
            }

            var staffIdentifier = ctx.Member?.Id.ToString() ?? ctx.User.Id.ToString();
            await balanceAdjustmentsService.RecordAdjustmentAsync(
                targetUser,
                staffIdentifier,
                BalanceAdjustmentType.AdminRemove,
                amountK,
                source: "AdminCommand");

            var updatedUser = await usersService.GetUserAsync(targetUser.Identifier);
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
