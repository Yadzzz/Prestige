using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Server.Client.Users;
using Server.Client.Transactions;
using Server.Client.Utils;
using Server.Infrastructure;
using Server.Infrastructure.Discord;

namespace Server.Communication.Discord.Commands
{
    public class WithdrawCommand : ModuleBase<SocketCommandContext>
    {
        private static readonly TimeSpan RateLimitInterval = TimeSpan.FromSeconds(1);
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, DateTime> LastUsed = new();

        [Command("w")]
        [Alias("withdraw", "wd")]
        public async Task Withdraw(string amount = null)
        {
            if (!await DiscordChannelPermissionService.EnforceWithdrawChannelAsync(Context))
            {
                return;
            }

            if (IsRateLimited(Context.User.Id))
            {
                await ReplyAsync("You're doing that too fast. Please wait a moment.");
                return;
            }

            if (string.IsNullOrWhiteSpace(amount))
            {
                await ReplyAsync("Please specify an amount. Usage: !w <amount> (e.g. !w 100m).");
                return;
            }

            var env = ServerEnvironment.GetServerEnvironment();
            var serverManager = env.ServerManager;
            var usersService = serverManager.UsersService;
            var transactionsService = serverManager.TransactionsService;

            var user = await usersService.EnsureUserAsync(Context.User.Id.ToString(), Context.User.Username, (Context.User as SocketGuildUser)?.DisplayName ?? Context.User.Username);
            if (user == null)
            {
                await ReplyAsync("Failed to load or create your user account. Please try again later.");
                return;
            }

            if (!GpParser.TryParseAmountInK(amount, out var amountK))
            {
                await ReplyAsync("Invalid amount. Examples: !w 100, !w 0.5, !w 1b, !w 1000m.");
                return;
            }

            // Minimum withdrawal is 10M => 10,000K internally
            const long minimumWithdrawalK = 10_000L;
            if (amountK < minimumWithdrawalK)
            {
                await ReplyAsync($"Minimum withdrawal is {GpFormatter.Format(minimumWithdrawalK)}.");
                return;
            }

            // Ensure the user has enough balance (stored in K)
            if (user.Balance < amountK)
            {
                await ReplyAsync("You don't have enough balance for this withdrawal.");
                return;
            }

            // Lock the withdrawal amount up-front, similar to stakes
            var balanceLocked = await usersService.RemoveBalanceAsync(user.Identifier, amountK);
            if (!balanceLocked)
            {
                await ReplyAsync("Failed to lock balance for this withdrawal. Please try again later.");
                return;
            }

            var transaction = await transactionsService.CreateWithdrawRequestAsync(user, amountK);
            if (transaction == null)
            {
                await ReplyAsync("Failed to create withdrawal request. Please try again later.");
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
            var remainingText = GpFormatter.Format(user.Balance - transaction.AmountK);

            var pendingEmbed = new EmbedBuilder()
                .WithTitle("Withdraw Request")
                .WithDescription("Your withdrawal request was submitted. Please choose a withdrawal method below.")
                .AddField("Member", Context.User.Username, true)
                .AddField("Amount", $"**{prettyAmount}**", true)
                .AddField("Remaining", $"**{remainingText}**", true)
                .WithColor(Color.Gold)
                .WithThumbnailUrl("https://i.imgur.com/A4tPGOW.gif")
                .WithCurrentTimestamp();

            var userComponents = new ComponentBuilder()
                .WithButton("In-game (5% fee)", $"tx_deposit_ingame_{transaction.Id}", ButtonStyle.Secondary, new Emoji("🎮"))
                .WithButton("Crypto (0% fee)", $"tx_deposit_crypto_{transaction.Id}", ButtonStyle.Secondary, new Emoji("🪙"));
                // .WithButton("Cancel", $"tx_usercancel_{transaction.Id}", ButtonStyle.Secondary, new Emoji("❌"));

            var userMessage = await ReplyAsync(embed: pendingEmbed.Build(), components: userComponents.Build());

            var staffChannel = await Context.Client.GetChannelAsync(DiscordIds.WithdrawStaffChannelId) as IMessageChannel;
            if (staffChannel != null)
            {
                var staffEmbed = new EmbedBuilder()
                    .WithTitle("New Withdrawal Request ⏳")
                    .WithDescription($"User: {(Context.User as SocketGuildUser)?.DisplayName ?? Context.User.Username} ({user.Identifier})\nAmount: **{prettyAmount}**\nTransaction ID: {transaction.Id}\nWithdraw Type: **Not selected**\nStatus: **PENDING**")
                    .WithColor(Color.Orange)
                    .WithCurrentTimestamp();

                var staffComponents = new ComponentBuilder()
                    .WithButton("Accept", $"tx_accept_{transaction.Id}", ButtonStyle.Success, new Emoji("✅"), disabled: true)
                    .WithButton("Cancel", $"tx_cancel_{transaction.Id}", ButtonStyle.Secondary, new Emoji("❌"))
                    .WithButton("Deny", $"tx_deny_{transaction.Id}", ButtonStyle.Danger, new Emoji("❌"));

                var staffMessage = await staffChannel.SendMessageAsync(
                    text: $"<@&{DiscordIds.StaffRoleId}>",
                    embed: staffEmbed.Build(),
                    components: staffComponents.Build());

                await transactionsService.UpdateTransactionMessagesAsync(
                    transaction.Id,
                    userMessage.Id,
                    userMessage.Channel.Id,
                    staffMessage.Id,
                    staffMessage.Channel.Id);
            }
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
