using System;
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
            }
            else
            {
                await HandleStaffTransactionAction(client, e);
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
                notes: null);

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

            var statusText = newStatus.ToString().ToUpperInvariant();
            var typeLabel = transaction.Type == TransactionType.Withdraw ? "Withdrawal" : "Deposit";

            usersService.TryGetUser(transaction.Identifier, out var updatedUser);
            var balanceText = updatedUser != null ? GpFormatter.Format(updatedUser.Balance) : null;

            var originalEmbed = e.Message.Embeds.Count > 0 ? e.Message.Embeds[0] : null;
            var updatedEmbed = new DiscordEmbedBuilder(originalEmbed ?? new DiscordEmbedBuilder())
                .WithTitle($"{typeLabel} Request - {statusText}");

            // Update or inject Status line in description to reflect new status
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
                new DiscordButtonComponent(ButtonStyle.Secondary, $"tx_cancel_{txId}", "Cancel", disabled: true, emoji: new DiscordComponentEmoji("🔁")),
                new DiscordButtonComponent(ButtonStyle.Danger, $"tx_deny_{txId}", "Deny", disabled: true, emoji: new DiscordComponentEmoji("❌"))
            };

            var responseBuilder = new DiscordInteractionResponseBuilder()
                .AddEmbed(updatedEmbed)
                .AddComponents(components);

            await e.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, responseBuilder);

            if (transaction.UserChannelId.HasValue && transaction.UserMessageId.HasValue)
            {
                try
                {
                    var userChannel = await client.GetChannelAsync(transaction.UserChannelId.Value);
                    var userMessage = await userChannel.GetMessageAsync(transaction.UserMessageId.Value);

                    var userEmbedBuilder = new DiscordEmbedBuilder(userMessage.Embeds.Count > 0 ? userMessage.Embeds[0] : new DiscordEmbedBuilder());
                    var statusIcon = newStatus switch
                    {
                        TransactionStatus.Accepted => "✅",
                        TransactionStatus.Cancelled => "🔁",
                        TransactionStatus.Denied => "❌",
                        _ => string.Empty
                    };
                    var userTypeLabel = transaction.Type == TransactionType.Withdraw ? "Withdrawal" : "Deposit";
                    userEmbedBuilder.Title = $"{userTypeLabel} Request {statusIcon}".Trim();

                    var userDesc = userEmbedBuilder.Description ?? string.Empty;
                    if (!string.IsNullOrEmpty(userDesc))
                    {
                        userDesc = userDesc.Replace("pending", statusText.ToLowerInvariant());
                    }

                    if (!string.IsNullOrEmpty(balanceText))
                    {
                        var goldIcon = "💰"; // RuneScape-style gold icon approximation
                        var balanceLine = $"{goldIcon} Balance: **{balanceText}**";

                        if (string.IsNullOrEmpty(userDesc))
                        {
                            // No main text, just show balance
                            userDesc = balanceLine;
                        }
                        else if (!userDesc.Contains("Balance:", StringComparison.OrdinalIgnoreCase))
                        {
                            // Add a blank line, then the balance line for spacing
                            userDesc += "\n\n" + balanceLine;
                        }
                    }

                    userEmbedBuilder.Description = userDesc;

                    await userMessage.ModifyAsync(builder =>
                    {
                        builder.Embed = userEmbedBuilder.Build();
                    });
                }
                catch
                {
                    // ignore if we can't edit the user message
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

            if (transaction.UserChannelId.HasValue && transaction.UserMessageId.HasValue)
            {
                try
                {
                    var userChannel = await client.GetChannelAsync(transaction.UserChannelId.Value);
                    var userMessage = await userChannel.GetMessageAsync(transaction.UserMessageId.Value);

                    var userEmbedBuilder = new DiscordEmbedBuilder(userMessage.Embeds.Count > 0 ? userMessage.Embeds[0] : new DiscordEmbedBuilder());
                    var statusText = "CANCELLED";
                    var typeLabel = transaction.Type == TransactionType.Withdraw ? "Withdrawal" : "Deposit";
                    userEmbedBuilder.Title = $"{typeLabel} Request 🔁";
                    userEmbedBuilder.Description = userEmbedBuilder.Description?.Replace("pending", statusText.ToLowerInvariant());

                    await userMessage.ModifyAsync(builder =>
                    {
                        builder.Embed = userEmbedBuilder.Build();
                        builder.ClearComponents();
                    });
                }
                catch
                {
                    // ignore if we can't edit
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
                catch
                {
                    // ignore if we can't edit staff message
                }
            }

            await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent("Your deposit request has been cancelled.").AsEphemeral(true));
        }
    }
}
