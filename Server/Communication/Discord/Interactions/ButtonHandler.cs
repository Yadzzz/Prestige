using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Server.Client.Transactions;
using Server.Infrastructure;

namespace Server.Communication.Discord.Interactions
{
    public class ButtonHandler
    {
        public static async Task HandleButtons(DiscordClient client, ComponentInteractionCreateEventArgs e)
        {
            // Transaction actions: tx_accept_{id}, tx_cancel_{id}, tx_deny_{id}
            if (e.Id.StartsWith("tx_", StringComparison.OrdinalIgnoreCase))
            {
                await HandleTransactionAction(e);
            }
        }

        private static async Task HandleTransactionAction(ComponentInteractionCreateEventArgs e)
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

            // Update status in DB
            transactionsService.UpdateTransactionStatus(
                txId,
                newStatus,
                staffId: null,
                staffIdentifier: e.User.Id.ToString(),
                notes: null);

            // If accepted, credit user via UsersService
            if (newStatus == TransactionStatus.Accepted)
            {
                usersService.AddBalance(transaction.Identifier, transaction.AmountK);
            }

            var statusText = newStatus.ToString().ToUpperInvariant();

            var builder = new DiscordInteractionResponseBuilder()
                .WithContent($"Transaction `{txId}` is now **{statusText}**.")
                .AsEphemeral(true);

            await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, builder);
        }
    }
}
