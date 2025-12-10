using System;
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
        [Aliases("withdraw", "wd")]
        public async Task Withdraw(CommandContext ctx, string amount)
        {
            if (!await DiscordChannelPermissionService.EnforceWithdrawChannelAsync(ctx))
            {
                return;
            }

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

            if (!GpParser.TryParseAmountInK(amount, out var amountK))
            {
                await ctx.RespondAsync("Invalid amount. Examples: `!w 100`, `!w 0.5`, `!w 1b`, `!w 1000m`.");
                return;
            }

            // Minimum withdrawal is 10M => 10,000K internally
            const long minimumWithdrawalK = 10_000L;
            if (amountK < minimumWithdrawalK)
            {
                await ctx.RespondAsync($"Minimum withdrawal is {GpFormatter.Format(minimumWithdrawalK)}.");
                return;
            }

            // Ensure the user has enough balance (stored in K)
            if (user.Balance < amountK)
            {
                await ctx.RespondAsync("You don't have enough balance for this withdrawal.");
                return;
            }

            // Lock the withdrawal amount up-front, similar to stakes
            var balanceLocked = await usersService.RemoveBalanceAsync(user.Identifier, amountK);
            if (!balanceLocked)
            {
                await ctx.RespondAsync("Failed to lock balance for this withdrawal. Please try again later.");
                return;
            }

            var transaction = await transactionsService.CreateWithdrawRequestAsync(user, amountK);
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
                // rollback locked balance if transaction creation failed
                await usersService.AddBalanceAsync(user.Identifier, amountK);
                return;
            }

            var prettyAmount = GpFormatter.Format(transaction.AmountK);
            var balanceText = GpFormatter.Format(user.Balance);
            var remainingText = GpFormatter.Format(user.Balance - transaction.AmountK);

            var pendingEmbed = new DiscordEmbedBuilder()
                .WithTitle("Withdraw Request")
                .WithDescription("Your withdrawal request was submitted. Please choose a withdrawal method below.")
                .AddField("Member", ctx.User.Username, true)
                .AddField("Amount", $"**{prettyAmount}**", true)
                .AddField("Remaining", $"**{remainingText}**", true)
                .WithColor(DiscordColor.Gold)
                .WithThumbnail("https://i.imgur.com/A4tPGOW.gif")
                .WithTimestamp(DateTimeOffset.UtcNow);

            var ingameButton = new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"tx_deposit_ingame_{transaction.Id}", "In-game (5% fee)", emoji: new DiscordComponentEmoji("🎮"));
            var cryptoButton = new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"tx_deposit_crypto_{transaction.Id}", "Crypto (0% fee)", emoji: new DiscordComponentEmoji("🪙"));
            var userCancelButton = new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"tx_usercancel_{transaction.Id}", "Cancel", emoji: new DiscordComponentEmoji("❌"));

            var userMessage = await ctx.RespondAsync(new DiscordMessageBuilder()
                .AddEmbed(pendingEmbed)
                .AddComponents(ingameButton, cryptoButton /*, userCancelButton*/));

            var staffChannel = await ctx.Client.GetChannelAsync(DiscordIds.WithdrawStaffChannelId);

            var staffEmbed = new DiscordEmbedBuilder()
                .WithTitle("New Withdrawal Request ⏳")
                .WithDescription($"User: {ctx.Member.DisplayName} ({user.Identifier})\nAmount: **{prettyAmount}**\nTransaction ID: `{transaction.Id}`\nWithdraw Type: **Not selected**\nStatus: **PENDING**")
                .WithColor(DiscordColor.Orange)
                .WithTimestamp(DateTimeOffset.UtcNow);

            var acceptButton = new DiscordButtonComponent(DiscordButtonStyle.Success, $"tx_accept_{transaction.Id}", "Accept", disabled: true, emoji: new DiscordComponentEmoji("✅"));
            var cancelButton = new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"tx_cancel_{transaction.Id}", "Cancel", emoji: new DiscordComponentEmoji("❌"));
            var denyButton = new DiscordButtonComponent(DiscordButtonStyle.Danger, $"tx_deny_{transaction.Id}", "Deny", emoji: new DiscordComponentEmoji("❌"));

            var staffMessage = await staffChannel.SendMessageAsync(new DiscordMessageBuilder()
                .WithContent($"<@&{DiscordIds.StaffRoleId}>")
                .AddEmbed(staffEmbed)
                .AddComponents(acceptButton, cancelButton, denyButton));

            await transactionsService.UpdateTransactionMessagesAsync(
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

    }
}
