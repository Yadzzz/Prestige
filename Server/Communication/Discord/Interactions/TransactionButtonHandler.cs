using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Server.Client.Transactions;
using Server.Client.Users;
using Server.Client.Utils;
using Server.Infrastructure;
using Server.Infrastructure.Discord;
using Server.Communication.Discord.Commands;

namespace Server.Communication.Discord.Interactions
{
    public static class TransactionButtonHandler
    {
        public static async Task Handle(DiscordSocketClient client, SocketMessageComponent component)
        {
            if (RateLimiter.IsRateLimited(component.User.Id))
            {
                await component.RespondAsync("You're doing that too fast.", ephemeral: true);
                return;
            }

            if (component.Data.CustomId.StartsWith("tx_usercancel_", StringComparison.OrdinalIgnoreCase))
            {
                await HandleUserCancelAction(client, component);
                return;
            }

            if (component.Data.CustomId.StartsWith("tx_deposit_ingame_", StringComparison.OrdinalIgnoreCase) ||
                component.Data.CustomId.StartsWith("tx_deposit_crypto_", StringComparison.OrdinalIgnoreCase))
            {
                await HandleDepositTypeSelectionAsync(client, component);
            }
            else
            {
                await HandleStaffTransactionAction(client, component);
            }
        }

        private static async Task HandleDepositTypeSelectionAsync(DiscordSocketClient client, SocketMessageComponent component)
        {
            var parts = component.Data.CustomId.Split('_');
            if (parts.Length != 4)
                return;

            var typeToken = parts[2]; // ingame or crypto
            if (!int.TryParse(parts[3], out var txId))
                return;

            var env = ServerEnvironment.GetServerEnvironment();
            var transactionsService = env.ServerManager.TransactionsService;

            var transaction = await transactionsService.GetTransactionByIdAsync(txId);
            if (transaction == null || transaction.Type != TransactionType.Withdraw)
            {
                await component.RespondAsync("Withdrawal request not found.", ephemeral: true);
                return;
            }

            // Only the user who created this withdrawal may choose deposit type
            if (component.User == null || !string.Equals(component.User.Id.ToString(), transaction.Identifier, StringComparison.Ordinal))
            {
                await component.RespondAsync("This withdrawal doesn't belong to you.", ephemeral: true);
                return;
            }

            if (transaction.Status != TransactionStatus.Pending)
            {
                await component.RespondAsync("This withdrawal request has already been processed.", ephemeral: true);
                return;
            }

            var depositTypeText = typeToken.Equals("ingame", StringComparison.OrdinalIgnoreCase)
                ? "In-game (5% fee)"
                : "Crypto (0% fee)";

            // Pre-compute fee and net for staff display using transaction amount
            var amountK = transaction.AmountK;
            var prettyAmount = GpFormatter.Format(amountK);
            var feeK = typeToken.Equals("ingame", StringComparison.OrdinalIgnoreCase)
                ? (long)Math.Round(amountK * 0.05m, MidpointRounding.AwayFromZero)
                : 0L;
            var afterFeeK = amountK - feeK;
            var prettyFee = GpFormatter.Format(feeK);
            var prettyAfterFee = GpFormatter.Format(afterFeeK);

            // Persist fee_k when selection is made
            if (transaction.Type == TransactionType.Withdraw)
            {
                await transactionsService.UpdateTransactionFeeAsync(transaction.Id, feeK);
            }

            // Update staff message description with chosen deposit type and enable buttons
            if (transaction.StaffChannelId.HasValue && transaction.StaffMessageId.HasValue)
            {
                try
                {
                    var staffChannel = client.GetChannel(transaction.StaffChannelId.Value) as IMessageChannel;
                    if (staffChannel != null)
                    {
                        var staffMessage = await staffChannel.GetMessageAsync(transaction.StaffMessageId.Value) as IUserMessage;
                        if (staffMessage != null)
                        {
                            var originalEmbed = staffMessage.Embeds.Count > 0 ? staffMessage.Embeds.First() as Embed : null;
                            var desc = originalEmbed?.Description ?? string.Empty;

                            if (!string.IsNullOrEmpty(desc))
                            {
                                var lines = desc.Split('\n');
                                for (int i = 0; i < lines.Length; i++)
                                {
                                    if (lines[i].StartsWith("Withdraw Type:", StringComparison.OrdinalIgnoreCase))
                                    {
                                        lines[i] = $"Withdraw Type: **{depositTypeText}**";
                                    }
                                }

                                // If in-game is selected, append fee breakdown for staff only
                                if (typeToken.Equals("ingame", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Ensure the Amount line shows the formatted amount
                                    for (int i = 0; i < lines.Length; i++)
                                    {
                                        if (lines[i].StartsWith("Amount:", StringComparison.OrdinalIgnoreCase))
                                        {
                                            lines[i] = $"Amount: **{prettyAmount}**";
                                        }
                                    }

                                    // Remove any existing fee lines first
                                    lines = lines
                                        .Where(l => !l.StartsWith("Fee:", StringComparison.OrdinalIgnoreCase) &&
                                                    !l.StartsWith("Amount After Fees:", StringComparison.OrdinalIgnoreCase))
                                        .ToArray();

                                    var list = lines.ToList();
                                    list.Add($"Fee: **{prettyFee}**");
                                    list.Add($"Amount After Fees: **{prettyAfterFee}**");
                                    lines = list.ToArray();
                                }

                                desc = string.Join("\n", lines);
                            }

                            var updatedEmbed = originalEmbed != null ? originalEmbed.ToEmbedBuilder() : new EmbedBuilder();
                            updatedEmbed.WithDescription(desc);

                            var components = new ComponentBuilder()
                                .WithButton("Accept", $"tx_accept_{txId}", ButtonStyle.Success, new Emoji("✅"))
                                .WithButton("Cancel", $"tx_cancel_{txId}", ButtonStyle.Secondary, new Emoji("❌"))
                                .WithButton("Deny", $"tx_deny_{txId}", ButtonStyle.Danger, new Emoji("❌"))
                                .Build();

                            await staffMessage.ModifyAsync(msg =>
                            {
                                msg.Content = $"<@&{DiscordIds.StaffRoleId}>";
                                msg.Embed = updatedEmbed.Build();
                                msg.Components = components;
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    env.ServerManager.LoggerManager.LogError($"Failed to update staff deposit message after type selection: {ex}");
                    await env.ServerManager.LogsService.LogAsync(
                        source: nameof(TransactionButtonHandler),
                        level: "Error",
                        userIdentifier: transaction.Identifier,
                        action: "UpdateStaffDepositTypeFailed",
                        message: "Failed to update staff deposit message after type selection.",
                        exception: ex.ToString());
                }
            }

            // Update user message: keep Cancel button, update description, and adjust Amount for in-game to show net after fees
            try
            {
                if (transaction.UserChannelId.HasValue && transaction.UserMessageId.HasValue)
                {
                    var userChannel = client.GetChannel(transaction.UserChannelId.Value) as IMessageChannel;
                    if (userChannel != null)
                    {
                        var originalMessage = await userChannel.GetMessageAsync(transaction.UserMessageId.Value) as IUserMessage;
                        if (originalMessage != null)
                        {
                            var isIngame = typeToken.Equals("ingame", StringComparison.OrdinalIgnoreCase);
                            
                            var components = new ComponentBuilder()
                                .WithButton("In-game (5% fee)", $"tx_deposit_ingame_{txId}", isIngame ? ButtonStyle.Success : ButtonStyle.Secondary, new Emoji("🎮"), disabled: true)
                                .WithButton("Crypto (0% fee)", $"tx_deposit_crypto_{txId}", !isIngame ? ButtonStyle.Success : ButtonStyle.Secondary, new Emoji("🪙"), disabled: true)
                                .Build();

                            var originalEmbed = originalMessage.Embeds.Count > 0 ? originalMessage.Embeds.First() as Embed : null;
                            var embedBuilder = originalEmbed != null ? originalEmbed.ToEmbedBuilder() : new EmbedBuilder();

                            // Always change description text after method selection
                            embedBuilder.WithDescription("Your withdrawal request was sent for staff review.");

                            // If in-game, update Amount field for the user to show net (after fees)
                            if (typeToken.Equals("ingame", StringComparison.OrdinalIgnoreCase) && originalEmbed != null)
                            {
                                // ClearFields is not directly available on EmbedBuilder in Discord.Net to remove all, 
                                // but we can re-add them. Or we can manipulate the Fields list.
                                // Actually EmbedBuilder.Fields is a List<EmbedFieldBuilder>.
                                var fields = embedBuilder.Fields;
                                foreach (var f in fields)
                                {
                                    if (string.Equals(f.Name, "Amount", StringComparison.OrdinalIgnoreCase))
                                    {
                                        f.Value = prettyAfterFee;
                                    }
                                }
                            }

                            await originalMessage.ModifyAsync(msg =>
                            {
                                msg.Embed = embedBuilder.Build();
                                msg.Components = components;
                            });
                        }
                    }
                }

                await component.RespondAsync($"You selected **{depositTypeText}** for this withdrawal.", ephemeral: true);
            }
            catch (Exception ex)
            {
                env.ServerManager.LoggerManager.LogError($"Failed to update user deposit message after type selection: {ex}");
                await env.ServerManager.LogsService.LogAsync(
                    source: nameof(TransactionButtonHandler),
                    level: "Error",
                    userIdentifier: transaction.Identifier,
                    action: "UpdateUserDepositTypeFailed",
                    message: "Failed to update user deposit message after type selection.",
                    exception: ex.ToString());
            }
        }

        private static async Task HandleStaffTransactionAction(DiscordSocketClient client, SocketMessageComponent component)
        {
            // Security: Ensure the user clicking the button has the Staff role
            var guild = (component.Channel as SocketGuildChannel)?.Guild;
            var member = guild?.GetUser(component.User.Id);
            
            if (member == null || !member.IsStaff())
            {
                await component.RespondAsync("You are not authorized to perform this action.", ephemeral: true);
                return;
            }

            var parts = component.Data.CustomId.Split('_');
            if (parts.Length != 3)
                return;

            var action = parts[1];
            if (!int.TryParse(parts[2], out var txId))
                return;

            var env = ServerEnvironment.GetServerEnvironment();
            var transactionsService = env.ServerManager.TransactionsService;
            var usersService = env.ServerManager.UsersService;

            var transaction = await transactionsService.GetTransactionByIdAsync(txId);
            if (transaction == null)
            {
                await component.RespondAsync("Transaction not found.", ephemeral: true);
                return;
            }

            var newStatus = transaction.Status;
            if (action.Equals("accept", StringComparison.OrdinalIgnoreCase))
            {
                newStatus = TransactionStatus.Accepted;
            }
            else if (action.Equals("cancel", StringComparison.OrdinalIgnoreCase))
            {
                newStatus = TransactionStatus.Cancelled;
            }
            else if (action.Equals("deny", StringComparison.OrdinalIgnoreCase))
            {
                newStatus = TransactionStatus.Denied;
            }
            else
            {
                return;
            }

            if (transaction.Status != TransactionStatus.Pending)
            {
                await component.RespondAsync("This transaction has already been processed.", ephemeral: true);
                return;
            }

            await transactionsService.UpdateTransactionStatusAsync(
                txId,
                newStatus,
                staffId: null,
                staffIdentifier: component.User.Id.ToString(),
                notes: string.Empty);

            if (newStatus == TransactionStatus.Accepted)
            {
                var balanceAdjustmentsService = env.ServerManager.BalanceAdjustmentsService;
                var targetUser = await usersService.GetUserAsync(transaction.Identifier);

                if (transaction.Type == TransactionType.Deposit)
                {
                    await usersService.AddBalanceAsync(transaction.Identifier, transaction.AmountK);
                    if (targetUser != null)
                    {
                        await balanceAdjustmentsService.RecordAdjustmentAsync(
                            targetUser,
                            component.User.Id.ToString(),
                            BalanceAdjustmentType.Deposit,
                            transaction.AmountK,
                            source: "TransactionButtonHandler",
                            reason: $"Deposit Accepted (TxId: {txId})");
                    }
                }
                else if (transaction.Type == TransactionType.Withdraw)
                {
                    // For withdrawals, the amount was already locked when the request was created.
                    // On accept we don't change balance again; on cancel/deny we refund below.
                    if (targetUser != null)
                    {
                        await balanceAdjustmentsService.RecordAdjustmentAsync(
                            targetUser,
                            component.User.Id.ToString(),
                            BalanceAdjustmentType.Withdraw,
                            transaction.AmountK,
                            source: "TransactionButtonHandler",
                            reason: $"Withdrawal Accepted (TxId: {txId})");
                    }
                }
            }
            else if ((newStatus == TransactionStatus.Cancelled || newStatus == TransactionStatus.Denied)
                     && transaction.Type == TransactionType.Withdraw)
            {
                // Refund locked withdrawal amount on cancel/deny so user balance returns to original.
                await usersService.AddBalanceAsync(transaction.Identifier, transaction.AmountK);
            }

            await env.ServerManager.LogsService.LogAsync(
                source: nameof(TransactionButtonHandler),
                level: "Info",
                userIdentifier: transaction.Identifier,
                action: "TransactionResolved",
                message: $"Transaction resolved txId={txId} type={transaction.Type} status={newStatus} staff={component.User.Id}",
                exception: null,
                metadataJson: $"{{\"referenceId\":{txId},\"kind\":\"{transaction.Type}\",\"status\":\"{newStatus}\",\"staffId\":\"{component.User.Id}\"}}" );

            var statusText = newStatus.ToString().ToUpperInvariant();
            var typeLabel = transaction.Type == TransactionType.Withdraw ? "Withdrawal" : "Deposit";

            var updatedUser = await usersService.GetUserAsync(transaction.Identifier);
            var balanceText = updatedUser != null ? GpFormatter.Format(updatedUser.Balance) : null;

            var originalEmbed = component.Message.Embeds.Count > 0 ? component.Message.Embeds.First() as Embed : null;
            var updatedEmbed = originalEmbed != null ? originalEmbed.ToEmbedBuilder() : new EmbedBuilder();
            updatedEmbed.WithTitle($"{typeLabel} Request - {statusText}");

            // Update or inject Status line in description to reflect new status for staff
            var desc = originalEmbed?.Description ?? string.Empty;
            if (!string.IsNullOrEmpty(desc))
            {
                var newStatusLine = $"Status: **{statusText}**";
                if (desc.Contains("Status:", StringComparison.OrdinalIgnoreCase))
                {
                    var lines = desc.Split('\n');
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].StartsWith("Status:", StringComparison.OrdinalIgnoreCase))
                        {
                            lines[i] = newStatusLine;
                        }
                    }
                    desc = string.Join("\n", lines);
                }
                else
                {
                    if (!string.IsNullOrEmpty(desc))
                        desc += "\n" + newStatusLine;
                    else
                        desc = newStatusLine;
                }

                updatedEmbed.WithDescription(desc);
            }

            if (!string.IsNullOrEmpty(balanceText))
            {
                updatedEmbed.WithFooter($"Balance: {balanceText}");
            }

            var components = new ComponentBuilder()
                .WithButton("Accept", $"tx_accept_{txId}", ButtonStyle.Success, new Emoji("✅"), disabled: true)
                .WithButton("Cancel", $"tx_cancel_{txId}", ButtonStyle.Secondary, new Emoji("❌"), disabled: true)
                .WithButton("Deny", $"tx_deny_{txId}", ButtonStyle.Danger, new Emoji("❌"), disabled: true)
                .Build();

            await component.UpdateAsync(msg =>
            {
                msg.Embed = updatedEmbed.Build();
                msg.Components = components;
            });

            if (transaction.UserChannelId.HasValue)
            {
                try
                {
                    var userChannel = client.GetChannel(transaction.UserChannelId.Value) as IMessageChannel;
                    if (userChannel != null)
                    {
                        var isWithdraw = transaction.Type == TransactionType.Withdraw;

                        // For in-game withdrawals with a fee, show the net amount (after fees) to the user
                        var amountKForUser = transaction.AmountK;
                        if (isWithdraw && transaction.FeeK > 0 && transaction.FeeK < transaction.AmountK)
                        {
                            amountKForUser = transaction.AmountK - transaction.FeeK;
                        }

                        var amountText = GpFormatter.Format(amountKForUser);

                        // Colors per status
                        var color = newStatus switch
                        {
                            TransactionStatus.Accepted => Color.Green,
                            TransactionStatus.Denied => Color.Red,
                            TransactionStatus.Cancelled => Color.Orange,
                            _ => Color.Blue
                        };

                        var title = isWithdraw ? "Withdraw Request" : "Deposit Request";

                        var thumbnailUrl = isWithdraw
                            ? (newStatus == TransactionStatus.Accepted
                                ? "https://i.imgur.com/vHuCoye.gif"
                                : "https://i.imgur.com/lTUFG2C.gif")
                            : (newStatus == TransactionStatus.Accepted
                                ? "https://i.imgur.com/0qEQpNC.gif"
                                : "https://i.imgur.com/lTUFG2C.gif");

                        var processedEmbed = new EmbedBuilder()
                            .WithTitle(title)
                            .WithColor(color)
                            .WithThumbnailUrl(thumbnailUrl)
                            .WithCurrentTimestamp();

                        if (isWithdraw)
                        {
                            processedEmbed.WithDescription($"Your withdrawal request for **{amountText}** has been processed.");
                        }
                        else
                        {
                            var descStatus = newStatus switch
                            {
                                TransactionStatus.Accepted => "was processed.",
                                TransactionStatus.Denied => "was declined.",
                                TransactionStatus.Cancelled => "was cancelled.",
                                _ => "was processed."
                            };
                            processedEmbed.WithDescription($"Your deposit request was {descStatus}");
                        }

                        if (isWithdraw)
                        {
                            processedEmbed.AddField("Staff", component.User.Username, true);
                            if (!string.IsNullOrEmpty(balanceText))
                            {
                                processedEmbed.AddField("Balance", balanceText, true);
                            }
                            processedEmbed.AddField("Date", DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm"), true);
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(balanceText))
                            {
                                processedEmbed.AddField("Balance", balanceText, true);
                            }
                            processedEmbed.AddField("Deposit", amountText, true);
                            processedEmbed.AddField("Staff", component.User.Username, true);
                        }

                        await userChannel.SendMessageAsync(text: $"<@{transaction.Identifier}>", embed: processedEmbed.Build());

                        // Disable the original user cancel button on the pending transaction message, if present
                        if (transaction.UserMessageId.HasValue)
                        {
                            try
                            {
                                var originalMessage = await userChannel.GetMessageAsync(transaction.UserMessageId.Value) as IUserMessage;
                                if (originalMessage != null)
                                {
                                    var disabledCancel = new ButtonBuilder("Cancel", $"tx_usercancel_{transaction.Id}", ButtonStyle.Secondary, emote: new Emoji("❌")).WithDisabled(true);

                                    await originalMessage.ModifyAsync(msg =>
                                    {
                                        // Keep existing embeds
                                        // msg.Embeds is not settable directly to keep existing, but if we don't set it, it keeps existing?
                                        // No, ModifyAsync properties are optional. If null, no change.
                                        // But we want to clear components and add disabled cancel.
                                        msg.Components = new ComponentBuilder().WithButton(disabledCancel).Build();
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                var envOuter = ServerEnvironment.GetServerEnvironment();
                                envOuter.ServerManager.LoggerManager.LogError($"Failed to disable transaction user cancel button after staff action: {ex}");
                                await envOuter.ServerManager.LogsService.LogAsync(
                                    source: nameof(TransactionButtonHandler),
                                    level: "Error",
                                    userIdentifier: transaction.Identifier,
                                    action: "DisableTxUserCancelAfterStaffFailed",
                                    message: "Failed to disable transaction user cancel button after staff processed the transaction.",
                                    exception: ex.ToString());
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    var envOuter = ServerEnvironment.GetServerEnvironment();
                    envOuter.ServerManager.LoggerManager.LogError($"Failed to send processed transaction message to user: {ex}");
                    await envOuter.ServerManager.LogsService.LogAsync(
                        source: nameof(TransactionButtonHandler),
                        level: "Error",
                        userIdentifier: transaction.Identifier,
                        action: "SendProcessedTxToUserFailed",
                        message: "Failed to send processed transaction message to user.",
                        exception: ex.ToString());
                }
            }
        }

        private static async Task HandleUserCancelAction(DiscordSocketClient client, SocketMessageComponent component)
        {
            var parts = component.Data.CustomId.Split('_');
            if (parts.Length != 3)
                return;

            if (!int.TryParse(parts[2], out var txId))
                return;

            var env = ServerEnvironment.GetServerEnvironment();
            var transactionsService = env.ServerManager.TransactionsService;

            var transaction = await transactionsService.GetTransactionByIdAsync(txId);
            if (transaction == null)
            {
                await component.RespondAsync("Transaction not found.", ephemeral: true);
                return;
            }

            if (transaction.Status != TransactionStatus.Pending || transaction.Identifier != component.User.Id.ToString())
            {
                await component.RespondAsync("You cannot cancel this transaction.", ephemeral: true);
                return;
            }

            await transactionsService.UpdateTransactionStatusAsync(
                txId,
                TransactionStatus.Cancelled,
                staffId: null,
                staffIdentifier: component.User.Id.ToString(),
                notes: "Cancelled by user");

            // If this was a withdraw, refund the locked amount on user cancel
            if (transaction.Type == TransactionType.Withdraw)
            {
                var usersService = env.ServerManager.UsersService;
                await usersService.AddBalanceAsync(transaction.Identifier, transaction.AmountK);
            }

            await env.ServerManager.LogsService.LogAsync(
                source: nameof(TransactionButtonHandler),
                level: "Info",
                userIdentifier: transaction.Identifier,
                action: "TransactionUserCancelled",
                message: $"Transaction user-cancelled txId={txId} type={transaction.Type} amountK={transaction.AmountK}",
                exception: null,
                metadataJson: $"{{\"referenceId\":{txId},\"kind\":\"{transaction.Type}\",\"amountK\":{transaction.AmountK},\"cancelledBy\":\"User\"}}" );

            if (transaction.UserChannelId.HasValue)
            {
                try
                {
                    var userChannel = client.GetChannel(transaction.UserChannelId.Value) as IMessageChannel;
                    if (userChannel != null)
                    {
                        var typeLabel = transaction.Type == TransactionType.Withdraw ? "Withdrawal" : "Deposit";

                        var userEmbedBuilder = new EmbedBuilder()
                            .WithTitle($"{typeLabel} 🔁")
                            .WithDescription($"Your {typeLabel.ToLowerInvariant()} request was cancelled.")
                            .WithColor(Color.Orange)
                            .WithThumbnailUrl(transaction.Type == TransactionType.Withdraw ? "https://i.imgur.com/A4tPGOW.gif" : "https://i.imgur.com/DHXgtn5.gif")
                            .WithCurrentTimestamp();

                        await userChannel.SendMessageAsync(text: $"<@{transaction.Identifier}>", embed: userEmbedBuilder.Build());

                        // Disable cancel button on the original user message, if present
                        if (transaction.UserMessageId.HasValue)
                        {
                            try
                            {
                                var originalMessage = await userChannel.GetMessageAsync(transaction.UserMessageId.Value) as IUserMessage;
                                if (originalMessage != null)
                                {
                                    var disabledCancel = new ButtonBuilder("Cancel", $"tx_usercancel_{transaction.Id}", ButtonStyle.Secondary, emote: new Emoji("🔁")).WithDisabled(true);

                                    await originalMessage.ModifyAsync(msg =>
                                    {
                                        msg.Components = new ComponentBuilder().WithButton(disabledCancel).Build();
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                ServerEnvironment.GetServerEnvironment().ServerManager.LoggerManager.LogError($"Failed to disable transaction user cancel button on user cancel: {ex}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ServerEnvironment.GetServerEnvironment().ServerManager.LoggerManager.LogError($"Failed to send transaction cancel message to user: {ex}");
                }
            }

            if (transaction.StaffChannelId.HasValue && transaction.StaffMessageId.HasValue)
            {
                try
                {
                    var staffChannel = client.GetChannel(transaction.StaffChannelId.Value) as IMessageChannel;
                    if (staffChannel != null)
                    {
                        var staffMessage = await staffChannel.GetMessageAsync(transaction.StaffMessageId.Value) as IUserMessage;
                        if (staffMessage != null)
                        {
                            var originalEmbed = staffMessage.Embeds.Count > 0 ? staffMessage.Embeds.First() as Embed : null;
                            var statusText = "CANCELLED";
                            var typeLabel = transaction.Type == TransactionType.Withdraw ? "Withdrawal" : "Deposit";
                            var updatedEmbed = originalEmbed != null ? originalEmbed.ToEmbedBuilder() : new EmbedBuilder();
                            updatedEmbed.WithTitle($"{typeLabel} Request - {statusText}");

                            // Also update Status: line inside the description so staff see correct status
                            var desc = originalEmbed?.Description ?? string.Empty;
                            if (!string.IsNullOrEmpty(desc))
                            {
                                var newStatusLine = $"Status: **{statusText}**";
                                if (desc.Contains("Status:", StringComparison.OrdinalIgnoreCase))
                                {
                                    var lines = desc.Split('\n');
                                    for (int i = 0; i < lines.Length; i++)
                                    {
                                        if (lines[i].StartsWith("Status:", StringComparison.OrdinalIgnoreCase))
                                        {
                                            lines[i] = newStatusLine;
                                        }
                                    }
                                    desc = string.Join("\n", lines);
                                }
                                else
                                {
                                    if (!string.IsNullOrEmpty(desc))
                                        desc += "\n" + newStatusLine;
                                    else
                                        desc = newStatusLine;
                                }

                                updatedEmbed.WithDescription(desc);
                            }

                            var components = new ComponentBuilder()
                                .WithButton("Accept", $"tx_accept_{txId}", ButtonStyle.Success, new Emoji("✅"), disabled: true)
                                .WithButton("Cancel", $"tx_cancel_{txId}", ButtonStyle.Secondary, new Emoji("🔁"), disabled: true)
                                .WithButton("Deny", $"tx_deny_{txId}", ButtonStyle.Danger, new Emoji("❌"), disabled: true)
                                .Build();

                            await staffMessage.ModifyAsync(msg =>
                            {
                                msg.Embed = updatedEmbed.Build();
                                msg.Components = components;
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    ServerEnvironment.GetServerEnvironment().ServerManager.LoggerManager.LogError($"Failed to update staff transaction message on user cancel: {ex}");
                }
            }

            await component.RespondAsync("Your deposit request has been cancelled.", ephemeral: true);
        }
    }
}
