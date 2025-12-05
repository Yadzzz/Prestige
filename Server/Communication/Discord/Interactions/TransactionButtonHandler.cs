using System;
using System.Globalization;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Server.Client.Transactions;
using Server.Client.Users;
using Server.Client.Utils;
using Server.Infrastructure;

namespace Server.Communication.Discord.Interactions
{
    public static class TransactionButtonHandler
    {
        public static async Task Handle(DiscordClient client, ComponentInteractionCreateEventArgs e)
        {
            if (e.Id.StartsWith("tx_usercancel_", StringComparison.OrdinalIgnoreCase))
            {
                await HandleUserCancelAction(client, e);
                return;
            }

            if (e.Id.StartsWith("tx_deposit_ingame_", StringComparison.OrdinalIgnoreCase) ||
                e.Id.StartsWith("tx_deposit_crypto_", StringComparison.OrdinalIgnoreCase))
            {
                await HandleDepositTypeSelectionAsync(client, e);
            }
            else
            {
                await HandleStaffTransactionAction(client, e);
            }
        }

        private static async Task HandleDepositTypeSelectionAsync(DiscordClient client, ComponentInteractionCreateEventArgs e)
        {
            var parts = e.Id.Split('_');
            if (parts.Length != 4)
                return;

            var typeToken = parts[2]; // ingame or crypto
            if (!int.TryParse(parts[3], out var txId))
                return;

            var env = ServerEnvironment.GetServerEnvironment();
            var transactionsService = env.ServerManager.TransactionsService;

            var transaction = transactionsService.GetTransactionById(txId);
            if (transaction == null || transaction.Type != TransactionType.Withdraw)
            {
                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("Withdrawal request not found.").AsEphemeral(true));
                return;
            }

            if (transaction.Status != TransactionStatus.Pending)
            {
                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("This withdrawal request has already been processed.").AsEphemeral(true));
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
                transactionsService.UpdateTransactionFee(transaction.Id, feeK);
            }

            // Update staff message description with chosen deposit type and enable buttons
            if (transaction.StaffChannelId.HasValue && transaction.StaffMessageId.HasValue)
            {
                try
                {
                    var staffChannel = await client.GetChannelAsync(transaction.StaffChannelId.Value);
                    var staffMessage = await staffChannel.GetMessageAsync(transaction.StaffMessageId.Value);

                    var originalEmbed = staffMessage.Embeds.Count > 0 ? staffMessage.Embeds[0] : null;
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

                    var updatedEmbed = new DiscordEmbedBuilder(originalEmbed ?? new DiscordEmbedBuilder())
                        .WithDescription(desc);

                    var components = new DiscordComponent[]
                    {
                        new DiscordButtonComponent(ButtonStyle.Success, $"tx_accept_{txId}", "Accept", disabled: false, emoji: new DiscordComponentEmoji("✅")),
                        new DiscordButtonComponent(ButtonStyle.Secondary, $"tx_cancel_{txId}", "Cancel", disabled: false, emoji: new DiscordComponentEmoji("❌")),
                        new DiscordButtonComponent(ButtonStyle.Danger, $"tx_deny_{txId}", "Deny", disabled: false, emoji: new DiscordComponentEmoji("❌"))
                    };

                    await staffMessage.ModifyAsync(builder =>
                    {
                        builder.Embed = updatedEmbed.Build();
                        builder.ClearComponents();
                        builder.AddComponents(components);
                    });
                }
                catch (Exception ex)
                {
                    env.ServerManager.LoggerManager.LogError($"Failed to update staff deposit message after type selection: {ex}");
                    env.ServerManager.LogsService.Log(
                        source: nameof(TransactionButtonHandler),
                        level: "Error",
                        userIdentifier: transaction.Identifier,
                        action: "UpdateStaffDepositTypeFailed",
                        message: "Failed to update staff deposit message after type selection.",
                        exception: ex.ToString());
                }
            }

            // Disable user buttons
            try
            {
                if (transaction.UserChannelId.HasValue && transaction.UserMessageId.HasValue)
                {
                    var userChannel = await client.GetChannelAsync(transaction.UserChannelId.Value);
                    var originalMessage = await userChannel.GetMessageAsync(transaction.UserMessageId.Value);

                    var ingameDisabled = new DiscordButtonComponent(ButtonStyle.Success, $"tx_deposit_ingame_{txId}", "In-game (5% fee)", disabled: true, emoji: new DiscordComponentEmoji("🎮"));
                    var cryptoDisabled = new DiscordButtonComponent(ButtonStyle.Secondary, $"tx_deposit_crypto_{txId}", "Crypto (0% fee)", disabled: true, emoji: new DiscordComponentEmoji("🪙"));

                    var originalEmbed = originalMessage.Embeds.Count > 0 ? originalMessage.Embeds[0] : null;
                    var updatedEmbed = new DiscordEmbedBuilder(originalEmbed ?? new DiscordEmbedBuilder())
                        .WithDescription("Your withdrawal request was sent for staff review.");

                    await originalMessage.ModifyAsync(builder =>
                    {
                        builder.Embed = updatedEmbed.Build();
                        builder.ClearComponents();
                        builder.AddComponents(ingameDisabled, cryptoDisabled);
                    });
                }

                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent($"You selected **{depositTypeText}** for this withdrawal.")
                        .AsEphemeral(true));
            }
            catch (Exception ex)
            {
                env.ServerManager.LoggerManager.LogError($"Failed to update user deposit message after type selection: {ex}");
                env.ServerManager.LogsService.Log(
                    source: nameof(TransactionButtonHandler),
                    level: "Error",
                    userIdentifier: transaction.Identifier,
                    action: "UpdateUserDepositTypeFailed",
                    message: "Failed to update user deposit message after type selection.",
                    exception: ex.ToString());
            }
        }

        private static async Task HandleStaffTransactionAction(DiscordClient client, ComponentInteractionCreateEventArgs e)
        {
            var parts = e.Id.Split('_');
            if (parts.Length != 3)
                return;

            var action = parts[1];
            if (!int.TryParse(parts[2], out var txId))
                return;

            var env = ServerEnvironment.GetServerEnvironment();
            var transactionsService = env.ServerManager.TransactionsService;
            var usersService = env.ServerManager.UsersService;

            var transaction = transactionsService.GetTransactionById(txId);
            if (transaction == null)
            {
                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("Transaction not found.").AsEphemeral(true));
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
                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .WithContent("This transaction has already been processed.")
                        .AsEphemeral(true));
                return;
            }

            transactionsService.UpdateTransactionStatus(
                txId,
                newStatus,
                staffId: null,
                staffIdentifier: e.User.Id.ToString(),
                notes: string.Empty);

            if (newStatus == TransactionStatus.Accepted)
            {
                if (transaction.Type == TransactionType.Deposit)
                {
                    usersService.AddBalance(transaction.Identifier, transaction.AmountK);
                }
                else if (transaction.Type == TransactionType.Withdraw)
                {
                    usersService.RemoveBalance(transaction.Identifier, transaction.AmountK);
                }
            }

            env.ServerManager.LogsService.Log(
                source: nameof(TransactionButtonHandler),
                level: "Info",
                userIdentifier: transaction.Identifier,
                action: "TransactionResolved",
                message: $"Transaction resolved txId={txId} type={transaction.Type} status={newStatus} staff={e.User.Id}",
                exception: null,
                metadataJson: $"{{\"referenceId\":{txId},\"kind\":\"{transaction.Type}\",\"status\":\"{newStatus}\",\"staffId\":\"{e.User.Id}\"}}" );

            var statusText = newStatus.ToString().ToUpperInvariant();
            var typeLabel = transaction.Type == TransactionType.Withdraw ? "Withdrawal" : "Deposit";

            usersService.TryGetUser(transaction.Identifier, out var updatedUser);
            var balanceText = updatedUser != null ? GpFormatter.Format(updatedUser.Balance) : null;

            var originalEmbed = e.Message.Embeds.Count > 0 ? e.Message.Embeds[0] : null;
            var updatedEmbed = new DiscordEmbedBuilder(originalEmbed ?? new DiscordEmbedBuilder())
                .WithTitle($"{typeLabel} Request - {statusText}");

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

            var components = new DiscordComponent[]
            {
                new DiscordButtonComponent(ButtonStyle.Success, $"tx_accept_{txId}", "Accept", disabled: true, emoji: new DiscordComponentEmoji("✅")),
                new DiscordButtonComponent(ButtonStyle.Secondary, $"tx_cancel_{txId}", "Cancel", disabled: true, emoji: new DiscordComponentEmoji("❌")),
                new DiscordButtonComponent(ButtonStyle.Danger, $"tx_deny_{txId}", "Deny", disabled: true, emoji: new DiscordComponentEmoji("❌"))
            };

            var responseBuilder = new DiscordInteractionResponseBuilder()
                .AddEmbed(updatedEmbed)
                .AddComponents(components);

            await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, responseBuilder);

            if (transaction.UserChannelId.HasValue)
            {
                try
                {
                    var userChannel = await client.GetChannelAsync(transaction.UserChannelId.Value);
                    var isWithdraw = transaction.Type == TransactionType.Withdraw;
                    var amountText = GpFormatter.Format(transaction.AmountK);

                    // Colors per status
                    var color = newStatus switch
                    {
                        TransactionStatus.Accepted => DiscordColor.Green,
                        TransactionStatus.Denied => DiscordColor.Red,
                        TransactionStatus.Cancelled => DiscordColor.Orange,
                        _ => DiscordColor.Blurple
                    };

                    var title = isWithdraw ? "Withdraw Request" : "Deposit Request";

                    var thumbnailUrl = isWithdraw
                        ? "https://i.imgur.com/A4tPGOW.gif"
                        : (newStatus == TransactionStatus.Accepted
                            ? "https://i.imgur.com/0qEQpNC.gif"
                            : "https://i.imgur.com/DHXgtn5.gif");

                    var processedEmbed = new DiscordEmbedBuilder()
                        .WithTitle(title)
                        .WithColor(color)
                        .WithThumbnail(thumbnailUrl)
                        .WithTimestamp(DateTimeOffset.UtcNow);

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
                        processedEmbed.AddField("Staff", e.User.Username, true);
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
                        processedEmbed.AddField("Staff", e.User.Username, true);
                    }

                    await userChannel.SendMessageAsync(new DiscordMessageBuilder()
                        .WithContent($"<@{transaction.Identifier}>")
                        .AddEmbed(processedEmbed));

                    // Disable the original user cancel button on the pending transaction message, if present
                    if (transaction.UserMessageId.HasValue)
                    {
                        try
                        {
                            var originalMessage = await userChannel.GetMessageAsync(transaction.UserMessageId.Value);

                            var disabledCancel = new DiscordButtonComponent(
                                ButtonStyle.Secondary,
                                $"tx_usercancel_{transaction.Id}",
                                "Cancel",
                                disabled: true,
                                emoji: new DiscordComponentEmoji("❌"));

                            await originalMessage.ModifyAsync(builder =>
                            {
                                builder.Embed = originalMessage.Embeds.Count > 0 ? originalMessage.Embeds[0] : null;
                                builder.ClearComponents();
                                builder.AddComponents(disabledCancel);
                            });
                        }
                        catch (Exception ex)
                        {
                            var envOuter = ServerEnvironment.GetServerEnvironment();
                            envOuter.ServerManager.LoggerManager.LogError($"Failed to disable transaction user cancel button after staff action: {ex}");
                            envOuter.ServerManager.LogsService.Log(
                                source: nameof(TransactionButtonHandler),
                                level: "Error",
                                userIdentifier: transaction.Identifier,
                                action: "DisableTxUserCancelAfterStaffFailed",
                                message: "Failed to disable transaction user cancel button after staff processed the transaction.",
                                exception: ex.ToString());
                        }
                    }
                }
                catch (Exception ex)
                {
                    var envOuter = ServerEnvironment.GetServerEnvironment();
                    envOuter.ServerManager.LoggerManager.LogError($"Failed to send processed transaction message to user: {ex}");
                    envOuter.ServerManager.LogsService.Log(
                        source: nameof(TransactionButtonHandler),
                        level: "Error",
                        userIdentifier: transaction.Identifier,
                        action: "SendProcessedTxToUserFailed",
                        message: "Failed to send processed transaction message to user.",
                        exception: ex.ToString());
                }
            }
        }

        private static async Task HandleUserCancelAction(DiscordClient client, ComponentInteractionCreateEventArgs e)
        {
            var parts = e.Id.Split('_');
            if (parts.Length != 3)
                return;

            if (!int.TryParse(parts[2], out var txId))
                return;

            var env = ServerEnvironment.GetServerEnvironment();
            var transactionsService = env.ServerManager.TransactionsService;

            var transaction = transactionsService.GetTransactionById(txId);
            if (transaction == null)
            {
                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("Transaction not found.").AsEphemeral(true));
                return;
            }

            if (transaction.Status != TransactionStatus.Pending || transaction.Identifier != e.User.Id.ToString())
            {
                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("You cannot cancel this transaction.").AsEphemeral(true));
                return;
            }

            transactionsService.UpdateTransactionStatus(
                txId,
                TransactionStatus.Cancelled,
                staffId: null,
                staffIdentifier: e.User.Id.ToString(),
                notes: "Cancelled by user");

            env.ServerManager.LogsService.Log(
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
                    var userChannel = await client.GetChannelAsync(transaction.UserChannelId.Value);
                    var typeLabel = transaction.Type == TransactionType.Withdraw ? "Withdrawal" : "Deposit";

                    var userEmbedBuilder = new DiscordEmbedBuilder()
                        .WithTitle($"{typeLabel} 🔁")
                        .WithDescription($"Your {typeLabel.ToLowerInvariant()} request was cancelled.")
                        .WithColor(DiscordColor.Orange)
                        .WithThumbnail(transaction.Type == TransactionType.Withdraw ? "https://i.imgur.com/A4tPGOW.gif" : "https://i.imgur.com/DHXgtn5.gif")
                        .WithTimestamp(DateTimeOffset.UtcNow);

                    await userChannel.SendMessageAsync(new DiscordMessageBuilder()
                        .WithContent($"<@{transaction.Identifier}>")
                        .AddEmbed(userEmbedBuilder));

                    // Disable cancel button on the original user message, if present
                    if (transaction.UserMessageId.HasValue)
                    {
                        try
                        {
                            var originalMessage = await userChannel.GetMessageAsync(transaction.UserMessageId.Value);

                            var disabledCancel = new DiscordButtonComponent(
                                ButtonStyle.Secondary,
                                $"tx_usercancel_{transaction.Id}",
                                "Cancel",
                                disabled: true,
                                emoji: new DiscordComponentEmoji("🔁"));

                            await originalMessage.ModifyAsync(builder =>
                            {
                                builder.Embed = originalMessage.Embeds.Count > 0 ? originalMessage.Embeds[0] : null;
                                builder.ClearComponents();
                                builder.AddComponents(disabledCancel);
                            });
                        }
                        catch (Exception ex)
                        {
                            ServerEnvironment.GetServerEnvironment().ServerManager.LoggerManager.LogError($"Failed to disable transaction user cancel button on user cancel: {ex}");
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
                    var staffChannel = await client.GetChannelAsync(transaction.StaffChannelId.Value);
                    var staffMessage = await staffChannel.GetMessageAsync(transaction.StaffMessageId.Value);

                    var originalEmbed = staffMessage.Embeds.Count > 0 ? staffMessage.Embeds[0] : null;
                    var statusText = "CANCELLED";
                    var typeLabel = transaction.Type == TransactionType.Withdraw ? "Withdrawal" : "Deposit";
                    var updatedEmbed = new DiscordEmbedBuilder(originalEmbed ?? new DiscordEmbedBuilder())
                        .WithTitle($"{typeLabel} Request - {statusText}");

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

                    var components = new DiscordComponent[]
                    {
                        new DiscordButtonComponent(ButtonStyle.Success, $"tx_accept_{txId}", "Accept", disabled: true, emoji: new DiscordComponentEmoji("✅")),
                        new DiscordButtonComponent(ButtonStyle.Secondary, $"tx_cancel_{txId}", "Cancel", disabled: true, emoji: new DiscordComponentEmoji("🔁")),
                        new DiscordButtonComponent(ButtonStyle.Danger, $"tx_deny_{txId}", "Deny", disabled: true, emoji: new DiscordComponentEmoji("❌"))
                    };

                    await staffMessage.ModifyAsync(builder =>
                    {
                        builder.Embed = updatedEmbed.Build();
                        builder.ClearComponents();
                        builder.AddComponents(components);
                    });
                }
                catch (Exception ex)
                {
                    ServerEnvironment.GetServerEnvironment().ServerManager.LoggerManager.LogError($"Failed to update staff transaction message on user cancel: {ex}");
                }
            }

            await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent("Your deposit request has been cancelled.").AsEphemeral(true));
        }
    }
}
