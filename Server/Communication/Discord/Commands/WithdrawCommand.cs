using System;
using System.Globalization;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Server.Client.Users;
using Server.Client.Transactions;
using Server.Client.Utils;
using Server.Infrastructure;
using Server.Infrastructure.Discord;

namespace Server.Communication.Discord.Commands
{
    public class WithdrawCommand : BaseCommandModule
    {
        [Command("w")]
        [Aliases("withdraw")]
        public async Task Withdraw(CommandContext ctx, string amount)
        {
            var env = ServerEnvironment.GetServerEnvironment();
            var usersService = env.ServerManager.UsersService;
            var transactionsService = env.ServerManager.TransactionsService;

            var user = await usersService.EnsureUserAsync(ctx.User.Id.ToString(), ctx.User.Username, ctx.Member.DisplayName);
            if (user == null)
                return;

            if (!TryParseAmountInK(amount, out var amountK))
            {
                await ctx.RespondAsync("Invalid amount. Example: `!w 100` or `!w 0.5`");
                return;
            }

            // Ensure the user has enough balance (stored in K)
            if (user.Balance < amountK)
            {
                await ctx.RespondAsync("You don't have enough balance for this withdrawal.");
                return;
            }

            var transaction = transactionsService.CreateWithdrawRequest(user, amountK);
            if (transaction == null)
            {
                await ctx.RespondAsync("Failed to create withdrawal request. Please try again later.");
                return;
            }

            var prettyAmount = GpFormatter.Format(transaction.AmountK);
            var balanceText = GpFormatter.Format(user.Balance);
            var remainingText = GpFormatter.Format(user.Balance - transaction.AmountK);

            var pendingEmbed = new DiscordEmbedBuilder()
                .WithTitle("Withdraw Request")
                .WithDescription("Your withdrawal request was submitted. You will be notified once a staff member reviews it.")
                .AddField("Member", ctx.User.Username, true)
                .AddField("Amount", prettyAmount, true)
                .AddField("Remaining", remainingText, true)
                .WithColor(DiscordColor.Gold)
                .WithTimestamp(DateTimeOffset.UtcNow);

            var userCancelButton = new DiscordButtonComponent(ButtonStyle.Secondary, $"tx_usercancel_{transaction.Id}", "Cancel", emoji: new DiscordComponentEmoji("🔁"));

            var userMessage = await ctx.RespondAsync(new DiscordMessageBuilder()
                .AddEmbed(pendingEmbed)
                .AddComponents(userCancelButton));

            var staffChannel = await ctx.Client.GetChannelAsync(DiscordIds.WithdrawStaffChannelId);

            var staffEmbed = new DiscordEmbedBuilder()
                .WithTitle("New Withdrawal Request ⏳")
                .WithDescription($"User: {ctx.Member.DisplayName} ({user.Identifier})\nAmount: **{prettyAmount}**\nTransaction ID: `{transaction.Id}`\nStatus: **PENDING**")
                .WithColor(DiscordColor.Orange)
                .WithTimestamp(DateTimeOffset.UtcNow);

            var acceptButton = new DiscordButtonComponent(ButtonStyle.Success, $"tx_accept_{transaction.Id}", "Accept", emoji: new DiscordComponentEmoji("✅"));
            var cancelButton = new DiscordButtonComponent(ButtonStyle.Secondary, $"tx_cancel_{transaction.Id}", "Cancel", emoji: new DiscordComponentEmoji("🔁"));
            var denyButton = new DiscordButtonComponent(ButtonStyle.Danger, $"tx_deny_{transaction.Id}", "Deny", emoji: new DiscordComponentEmoji("❌"));

            var staffMessage = await staffChannel.SendMessageAsync(new DiscordMessageBuilder()
                .WithContent($"<@&{DiscordIds.StaffRoleId}>")
                .AddEmbed(staffEmbed)
                .AddComponents(acceptButton, cancelButton, denyButton));

            transactionsService.UpdateTransactionMessages(
                transaction.Id,
                userMessage.Id,
                userMessage.Channel.Id,
                staffMessage.Id,
                staffMessage.Channel.Id);
        }

        private bool TryParseAmountInK(string input, out long amountK)
        {
            amountK = 0;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            if (!decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out var amountM))
                return false;

            if (amountM <= 0)
                return false;

            var result = amountM * 1000m; // millions to thousands

            amountK = (long)Math.Round(result, MidpointRounding.AwayFromZero);
            return amountK > 0;
        }
    }
}
