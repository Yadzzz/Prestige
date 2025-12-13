using System;
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
        public async Task Deposit(CommandContext ctx, string amount = null)
        {
            if (!await DiscordChannelPermissionService.EnforceDepositChannelAsync(ctx))
            {
                return;
            }

            if (RateLimiter.IsRateLimited(ctx.User.Id))
            {
                await ctx.RespondAsync("You're doing that too fast. Please wait a moment.");
                return;
            }

            if (string.IsNullOrWhiteSpace(amount))
            {
                await ctx.RespondAsync("Please specify an amount. Usage: `!d <amount>` (e.g. `!d 100m`).");
                return;
            }

            var env = ServerEnvironment.GetServerEnvironment();
            var serverManager = env.ServerManager;
            var usersService = serverManager.UsersService;
            var transactionsService = serverManager.TransactionsService;

            var user = await usersService.EnsureUserAsync(ctx.User.Id.ToString(), ctx.User.Username, ctx.Member.DisplayName);
            if (user == null)
                return;

            if (!GpParser.TryParseAmountInK(amount, out var amountK))
            {
                await ctx.RespondAsync("Invalid amount. Examples: `!d 100`, `!d 0.5`, `!d 1b`, `!d 1000m`.");
                return;
            }

            // Minimum deposit 1M (1000K)
            if (amountK < GpFormatter.MinimumDepositAmountK)
            {
                await ctx.RespondAsync($"Minimum deposit is {GpFormatter.Format(GpFormatter.MinimumDepositAmountK)}.");
                return;
            }

            var transaction = await transactionsService.CreateDepositRequestAsync(user, amountK);
            if (transaction == null)
            {
                await ctx.RespondAsync("Failed to create deposit request. Please try again later.");
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

            var pendingEmbed = new DiscordEmbedBuilder()
                .WithTitle("Deposit Request")
                .WithDescription("Your deposit request was sent for staff review.")
                .AddField("Balance", $"**{balanceText}**", true)
                .AddField("Deposit", $"**{prettyAmount}**", true)
                .AddField("Expected Balance", $"**{expectedBalanceText}**", true)
                .WithColor(DiscordColor.Gold)
                .WithThumbnail("https://i.imgur.com/1hkVfFD.gif")
                .WithTimestamp(DateTimeOffset.UtcNow);

            var userCancelButton = new DiscordButtonComponent(
                DiscordButtonStyle.Secondary,
                $"tx_usercancel_{transaction.Id}",
                "Cancel",
                emoji: new DiscordComponentEmoji("❌"));

            var userMessage = await ctx.RespondAsync(new DiscordMessageBuilder()
                .AddEmbed(pendingEmbed));
                //.AddComponents(userCancelButton));

            // Send to staff channel
            var staffChannel = await ctx.Client.GetChannelAsync(DiscordIds.DepositStaffChannelId);

            var staffEmbed = new DiscordEmbedBuilder()
                .WithTitle("New Deposit Request ⏳")
                .WithDescription($"User: {ctx.Member.DisplayName} ({user.Identifier})\nAmount: **{prettyAmount}**\nTransaction ID: `{transaction.Id}`\nStatus: **PENDING**")
                .WithColor(DiscordColor.Orange)
                .WithTimestamp(DateTimeOffset.UtcNow);

            var acceptButton = new DiscordButtonComponent(DiscordButtonStyle.Success, $"tx_accept_{transaction.Id}", "Accept", emoji: new DiscordComponentEmoji("✅"));
            var cancelButton = new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"tx_cancel_{transaction.Id}", "Cancel", emoji: new DiscordComponentEmoji("❌"));
            var denyButton = new DiscordButtonComponent(DiscordButtonStyle.Danger, $"tx_deny_{transaction.Id}", "Deny", emoji: new DiscordComponentEmoji("❌"));

            var staffMessage = await staffChannel.SendMessageAsync(new DiscordMessageBuilder()
                .WithContent($"<@&{DiscordIds.StaffRoleId}>")
                .AddEmbed(staffEmbed)
                .AddActionRowComponent(new[] { acceptButton, cancelButton, denyButton }));

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
