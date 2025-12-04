using System;
using System.Globalization;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Server.Client.Users;
using Server.Client.Transactions;
using Server.Client.Utils;
using Server.Infrastructure;
using Server.Infrastructure.Discord;
using DSharpPlus;

namespace Server.Communication.Discord.Commands
{
    public class DepositCommand : BaseCommandModule
    {
        [Command("d")]
        [Aliases("deposit")] 
        public async Task Deposit(CommandContext ctx, string amount)
        {
            var env = ServerEnvironment.GetServerEnvironment();
            var usersService = env.ServerManager.UsersService;
            var transactionsService = env.ServerManager.TransactionsService;

            var user = await usersService.EnsureUserAsync(ctx.User.Id.ToString(), ctx.User.Username, ctx.Member.DisplayName);
            if (user == null)
                return;

            if (!TryParseAmountInK(amount, out var amountK))
            {
                await ctx.RespondAsync("Invalid amount. Example: `!d 100` or `!d 0.5`");
                return;
            }

            var transaction = transactionsService.CreateDepositRequest(user, amountK);
            if (transaction == null)
            {
                await ctx.RespondAsync("Failed to create deposit request. Please try again later.");
                return;
            }

            var prettyAmount = GpFormatter.Format(transaction.AmountK);

            var pendingEmbed = new DiscordEmbedBuilder()
                .WithTitle("Deposit Request Created ⏳")
                .WithDescription($"{ctx.Member.DisplayName}, your deposit of **{prettyAmount}** is now **pending**.")
                .WithColor(DiscordColor.Orange)
                .WithTimestamp(DateTimeOffset.UtcNow);

            var userCancelButton = new DiscordButtonComponent(ButtonStyle.Secondary, $"tx_usercancel_{transaction.Id}", "Cancel", emoji: new DiscordComponentEmoji("🔁"));

            // Send user message and capture its ID (no mention on initial create)
            var userMessage = await ctx.RespondAsync(new DiscordMessageBuilder()
                .AddEmbed(pendingEmbed)
                .AddComponents(userCancelButton));

            // Send to staff channel
            var staffChannel = await ctx.Client.GetChannelAsync(DiscordIds.DepositWithdrawStaffChannelId);

            var staffEmbed = new DiscordEmbedBuilder()
                .WithTitle("New Deposit Request ⏳")
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

            // Persist message/channel IDs so we can update messages on status changes
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
