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
    public class DepositCommand : ModuleBase<SocketCommandContext>
    {
        [Command("d")]
        [Alias("deposit")] 
        public async Task Deposit(string amount = null)
        {
            if (!await DiscordChannelPermissionService.EnforceDepositChannelAsync(Context))
            {
                return;
            }

            if (RateLimiter.IsRateLimited(Context.User.Id))
            {
                await ReplyAsync("You're doing that too fast. Please wait a moment.");
                return;
            }

            if (string.IsNullOrWhiteSpace(amount))
            {
                await ReplyAsync("Please specify an amount. Usage: `!d <amount>` (e.g. `!d 100m`).");
                return;
            }

            var env = ServerEnvironment.GetServerEnvironment();
            var serverManager = env.ServerManager;
            var usersService = serverManager.UsersService;
            var transactionsService = serverManager.TransactionsService;

            var user = await usersService.EnsureUserAsync(Context.User.Id.ToString(), Context.User.Username, (Context.User as SocketGuildUser)?.DisplayName ?? Context.User.Username);
            if (user == null)
                return;

            if (!GpParser.TryParseAmountInK(amount, out var amountK))
            {
                await ReplyAsync("Invalid amount. Examples: `!d 100`, `!d 0.5`, `!d 1b`, `!d 1000m`.");
                return;
            }

            var transaction = await transactionsService.CreateDepositRequestAsync(user, amountK);
            if (transaction == null)
            {
                await ReplyAsync("Failed to create deposit request. Please try again later.");
                await serverManager.LogsService.LogAsync(
                    source: nameof(DepositCommand),
                    level: "Error",
                    userIdentifier: user.Identifier,
                    action: "CreateDepositFailed",
                    message: $"Failed to create deposit for {user.Identifier} amountK={amountK}",
                    exception: null);
                return;
            }

            var prettyAmount = GpFormatter.Format(transaction.AmountK);
            var balanceText = GpFormatter.Format(user.Balance);
            var expectedBalanceText = GpFormatter.Format(user.Balance + transaction.AmountK);

            var pendingEmbed = new EmbedBuilder()
                .WithTitle("Deposit Request")
                .WithDescription("Your deposit request was sent for staff review.")
                .AddField("Balance", $"**{balanceText}**", true)
                .AddField("Deposit", $"**{prettyAmount}**", true)
                .AddField("Expected Balance", $"**{expectedBalanceText}**", true)
                .WithColor(Color.Gold)
                .WithThumbnailUrl("https://i.imgur.com/1hkVfFD.gif")
                .WithCurrentTimestamp();

            // var userCancelButton = new ButtonBuilder()
            //     .WithLabel("Cancel")
            //     .WithCustomId($"tx_usercancel_{transaction.Id}")
            //     .WithStyle(ButtonStyle.Secondary)
            //     .WithEmote(new Emoji("❌"));

            var userMessage = await ReplyAsync(embed: pendingEmbed.Build());
                // components: new ComponentBuilder().WithButton(userCancelButton).Build());

            // Send to staff channel
            var staffChannel = await Context.Client.GetChannelAsync(DiscordIds.DepositStaffChannelId) as IMessageChannel;
            if (staffChannel != null)
            {
                var staffEmbed = new EmbedBuilder()
                    .WithTitle("New Deposit Request ⏳")
                    .WithDescription($"User: {(Context.User as SocketGuildUser)?.DisplayName ?? Context.User.Username} ({user.Identifier})\nAmount: **{prettyAmount}**\nTransaction ID: `{transaction.Id}`\nStatus: **PENDING**")
                    .WithColor(Color.Orange)
                    .WithCurrentTimestamp();

                var staffComponents = new ComponentBuilder()
                    .WithButton("Accept", $"tx_accept_{transaction.Id}", ButtonStyle.Success, new Emoji("✅"))
                    .WithButton("Cancel", $"tx_cancel_{transaction.Id}", ButtonStyle.Secondary, new Emoji("❌"))
                    .WithButton("Deny", $"tx_deny_{transaction.Id}", ButtonStyle.Danger, new Emoji("❌"));

                var staffMessage = await staffChannel.SendMessageAsync(
                    text: $"<@&{DiscordIds.StaffRoleId}>",
                    embed: staffEmbed.Build(),
                    components: staffComponents.Build());

                // Persist message/channel IDs so we can update messages on status changes
                await transactionsService.UpdateTransactionMessagesAsync(
                    transaction.Id,
                    userMessage.Id,
                    userMessage.Channel.Id,
                    staffMessage.Id,
                    staffMessage.Channel.Id);
            }
        }

    }
}
