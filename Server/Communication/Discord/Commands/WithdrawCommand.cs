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
        private static readonly TimeSpan RateLimitInterval = TimeSpan.FromSeconds(1);
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, DateTime> LastUsed = new();

        [Command("w")]
        [Aliases("withdraw")]
        public async Task Withdraw(CommandContext ctx, string amount)
        {
            if (IsRateLimited(ctx.User.Id))
            {
                await ctx.RespondAsync("You're doing that too fast. Please wait a moment.");
                return;
            }

            var env = ServerEnvironment.GetServerEnvironment();
            var serverManager = env.ServerManager;
            var usersService = serverManager.UsersService;
            var transactionsService = serverManager.TransactionsService;

            var user = await usersService.EnsureUserAsync(ctx.User.Id.ToString(), ctx.User.Username, ctx.Member.DisplayName);
            if (user == null)
            {
                await ctx.RespondAsync("Failed to load or create your user account. Please try again later.");
                return;
            }

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
                serverManager.LogsService.Log(
                    source: nameof(WithdrawCommand),
                    level: "Error",
                    userIdentifier: user.Identifier,
                    action: "CreateWithdrawFailed",
                    message: $"Failed to create withdraw for {user.Identifier} amountK={amountK}",
                    exception: null);
                return;
            }

            var prettyAmount = GpFormatter.Format(transaction.AmountK);
            var balanceText = GpFormatter.Format(user.Balance);
            var remainingText = GpFormatter.Format(user.Balance - transaction.AmountK);

            var pendingEmbed = new DiscordEmbedBuilder()
                .WithTitle("Withdraw Request")
                .WithDescription("Your withdrawal request was submitted. Please choose a withdrawal method below.")
                .AddField("Member", ctx.User.Username, true)
                .AddField("Amount", prettyAmount, true)
                .AddField("Remaining", remainingText, true)
                .WithColor(DiscordColor.Gold)
                .WithThumbnail("https://i.imgur.com/A4tPGOW.gif")
                .WithTimestamp(DateTimeOffset.UtcNow);

            var ingameButton = new DiscordButtonComponent(ButtonStyle.Success, $"tx_deposit_ingame_{transaction.Id}", "In-game (5% fee)", emoji: new DiscordComponentEmoji("🎮"));
            var cryptoButton = new DiscordButtonComponent(ButtonStyle.Secondary, $"tx_deposit_crypto_{transaction.Id}", "Crypto (0% fee)", emoji: new DiscordComponentEmoji("🪙"));
            var userCancelButton = new DiscordButtonComponent(ButtonStyle.Secondary, $"tx_usercancel_{transaction.Id}", "Cancel", emoji: new DiscordComponentEmoji("❌"));

            var userMessage = await ctx.RespondAsync(new DiscordMessageBuilder()
                .AddEmbed(pendingEmbed)
                .AddComponents(ingameButton, cryptoButton, userCancelButton));

            var staffChannel = await ctx.Client.GetChannelAsync(DiscordIds.WithdrawStaffChannelId);

            var staffEmbed = new DiscordEmbedBuilder()
                .WithTitle("New Withdrawal Request ⏳")
                .WithDescription($"User: {ctx.Member.DisplayName} ({user.Identifier})\nAmount: **{prettyAmount}**\nTransaction ID: `{transaction.Id}`\nWithdraw Type: **Not selected**\nStatus: **PENDING**")
                .WithColor(DiscordColor.Orange)
                .WithTimestamp(DateTimeOffset.UtcNow);

            var acceptButton = new DiscordButtonComponent(ButtonStyle.Success, $"tx_accept_{transaction.Id}", "Accept", disabled: true, emoji: new DiscordComponentEmoji("✅"));
            var cancelButton = new DiscordButtonComponent(ButtonStyle.Secondary, $"tx_cancel_{transaction.Id}", "Cancel", emoji: new DiscordComponentEmoji("❌"));
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

        private bool IsRateLimited(ulong userId)
        {
            var now = DateTime.UtcNow;
            if (LastUsed.TryGetValue(userId, out var last) && (now - last) < RateLimitInterval)
            {
                return true;
            }

            LastUsed[userId] = now;
            return false;
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
