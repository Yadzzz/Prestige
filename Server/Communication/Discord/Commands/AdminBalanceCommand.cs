using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Server.Client.Users;
using Server.Client.Utils;
using Server.Client.Transactions;
using Server.Infrastructure;
using Server.Infrastructure.Discord;

namespace Server.Communication.Discord.Commands
{
    public class AdminBalanceCommand : ModuleBase<SocketCommandContext>
    {
        [Command("add")]
        public async Task AddAsync(string amount, SocketGuildUser member)
        {
            if (!(Context.User as SocketGuildUser).IsStaff())
            {
                await ReplyAsync("You are not authorized to use this command.");
                return;
            }
            await HandleBalanceChangeAsync(amount, member, BalanceAdjustmentType.AdminAdd);
        }

        [Command("gift")]
        public async Task GiftAsync(string amount, SocketGuildUser member)
        {
            if (!(Context.User as SocketGuildUser).IsStaff())
            {
                await ReplyAsync("You are not authorized to use this command.");
                return;
            }
            await HandleBalanceChangeAsync(amount, member, BalanceAdjustmentType.AdminGift);
        }

        [Command("remove")]
        public async Task RemoveAsync(string amount, SocketGuildUser member)
        {
            if (!(Context.User as SocketGuildUser).IsStaff())
            {
                await ReplyAsync("You are not authorized to use this command.");
                return;
            }
            await HandleBalanceRemovalAsync(amount, member);
        }

        private async Task HandleBalanceChangeAsync(string amount, SocketGuildUser targetMember, BalanceAdjustmentType adjustmentType)
        {
            var env = ServerEnvironment.GetServerEnvironment();
            var usersService = env.ServerManager.UsersService;
            var balanceAdjustmentsService = env.ServerManager.BalanceAdjustmentsService;

            if (!GpParser.TryParseAmountInK(amount, out var amountK))
            {
                await ReplyAsync("Invalid amount. Examples: `!add 10 @user`, `!add 0.5 @user`, `!add 1b @user`, `!add 1000m @user`.");
                return;
            }
            if (amountK <= 0)
            {
                await ReplyAsync("Amount must be greater than zero.");
                return;
            }

            var targetUser = await usersService.EnsureUserAsync(targetMember.Id.ToString(), targetMember.Username, targetMember.DisplayName);
            if (targetUser == null)
            {
                await ReplyAsync("Failed to resolve target user.");
                return;
            }

            var success = await usersService.AddBalanceAsync(targetUser.Identifier, amountK);
            if (!success)
            {
                await ReplyAsync("Failed to update user balance. Please try again later.");
                return;
            }

            var staffIdentifier = Context.User.Id.ToString();
            await balanceAdjustmentsService.RecordAdjustmentAsync(
                targetUser,
                staffIdentifier,
                adjustmentType,
                amountK,
                source: "AdminCommand");

            var updatedUser = await usersService.GetUserAsync(targetUser.Identifier);
            var prettyAmount = GpFormatter.Format(amountK);
            var newBalanceText = updatedUser != null ? GpFormatter.Format(updatedUser.Balance) : "unknown";

            var staffName = (Context.User as SocketGuildUser)?.DisplayName ?? Context.User.Username;

            var title = adjustmentType == BalanceAdjustmentType.AdminGift ? "Gifted" : "Added Balance";
            var embed = new EmbedBuilder()
                .WithTitle(title)
                .AddField("Amount", $"**{prettyAmount}**", true)
                .AddField("Member", targetMember.Username, true)
                .AddField("Staff", staffName, true)
                .WithColor(Color.Green)
                .WithThumbnailUrl(adjustmentType == BalanceAdjustmentType.AdminGift ? "https://i.imgur.com/vFstFPx.gif" : "https://i.imgur.com/0qEQpNC.gif")
                .WithFooter(ServerConfiguration.ServerName)
                .WithCurrentTimestamp();

            await ReplyAsync(message: targetMember.Mention, embed: embed.Build());
        }

        private async Task HandleBalanceRemovalAsync(string amount, SocketGuildUser targetMember)
        {
            var env = ServerEnvironment.GetServerEnvironment();
            var usersService = env.ServerManager.UsersService;
            var balanceAdjustmentsService = env.ServerManager.BalanceAdjustmentsService;

            if (!GpParser.TryParseAmountInK(amount, out var amountK))
            {
                await ReplyAsync("Invalid amount. Examples: `!remove 10 @user`, `!remove 0.5 @user`, `!remove 1b @user`, `!remove 1000m @user`.");
                return;
            }
            if (amountK <= 0)
            {
                await ReplyAsync("Amount must be greater than zero.");
                return;
            }

            var targetUser = await usersService.EnsureUserAsync(targetMember.Id.ToString(), targetMember.Username, targetMember.DisplayName);
            if (targetUser == null)
            {
                await ReplyAsync("Failed to resolve target user.");
                return;
            }

            var success = await usersService.RemoveBalanceAsync(targetUser.Identifier, amountK);
            if (!success)
            {
                await ReplyAsync("Failed to update user balance. Please try again later.");
                return;
            }

            var staffIdentifier = Context.User.Id.ToString();
            await balanceAdjustmentsService.RecordAdjustmentAsync(
                targetUser,
                staffIdentifier,
                BalanceAdjustmentType.AdminRemove,
                amountK,
                source: "AdminCommand");

            var updatedUser = await usersService.GetUserAsync(targetUser.Identifier);
            var prettyAmount = GpFormatter.Format(amountK);
            var newBalanceText = updatedUser != null ? GpFormatter.Format(updatedUser.Balance) : "unknown";

            var embed = new EmbedBuilder()
                .WithTitle("Balance Adjusted")
                .WithDescription($"Your balance was decreased by **{prettyAmount}** by staff.")
                .AddField("Balance", newBalanceText, true)
                .WithColor(Color.Red)
                .WithCurrentTimestamp();

            await ReplyAsync(message: targetMember.Mention, embed: embed.Build());
        }
    }
}
