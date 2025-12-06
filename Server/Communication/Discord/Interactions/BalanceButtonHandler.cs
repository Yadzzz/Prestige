using System;
using System.Linq;
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
    public static class BalanceButtonHandler
    {
        public static async Task Handle(DiscordClient client, ComponentInteractionCreateEventArgs e)
        {
            // All balance buttons are user-side; only the invoker may use them
            var identifier = e.User?.Id.ToString();
            if (string.IsNullOrEmpty(identifier))
                return;

            if (e.Id.StartsWith("bal_history", StringComparison.OrdinalIgnoreCase))
            {
                await HandleHistoryAsync(e);
                return;
            }

            if (e.Id.Equals("bal_deposit", StringComparison.OrdinalIgnoreCase))
            {
                await HandleDepositInfoAsync(e);
                return;
            }

            if (e.Id.Equals("bal_withdraw", StringComparison.OrdinalIgnoreCase))
            {
                await HandleWithdrawInfoAsync(e);
                return;
            }
        }

        private static async Task HandleHistoryAsync(ComponentInteractionCreateEventArgs e)
        {
            var env = ServerEnvironment.GetServerEnvironment();
            var transactionsService = env.ServerManager.TransactionsService;
            var usersService = env.ServerManager.UsersService;

            var identifier = e.User.Id.ToString();

            // Parse page from custom id: bal_history or bal_history_{page}
            var page = 1;
            var idParts = e.Id.Split('_');
            if (idParts.Length == 3 && int.TryParse(idParts[2], out var parsedPage) && parsedPage > 0)
            {
                page = parsedPage;
            }

            const int pageSize = 10;

            usersService.TryGetUser(identifier, out var user);
            var txs = transactionsService.GetTransactionsPageForUser(identifier, page, pageSize, out var totalCount);

            var embed = new DiscordEmbedBuilder()
                .WithTitle("Transaction History")
                .WithColor(DiscordColor.Blurple)
                .WithTimestamp(DateTimeOffset.UtcNow);

            if (user != null)
            {
                var balanceText = GpFormatter.Format(user.Balance);
                embed.WithDescription($"{e.User.Username}, your current balance is **{balanceText}**.");
            }

            if (txs == null || txs.Count == 0)
            {
                embed.AddField("History", "No transactions found yet.");
            }
            else
            {
                var lines = txs
                    .OrderByDescending(t => t.Id)
                    .Select(t =>
                    {
                        var typeLabel = t.Type == TransactionType.Withdraw ? "Withdraw" : "Deposit";
                        var status = t.Status.ToString();
                        var amount = GpFormatter.Format(t.AmountK);
                        var when = t.CreatedAt.ToString("yyyy-MM-dd HH:mm");
                        return $"`#{t.Id}` {typeLabel} **{amount}** • {status} • {when}";
                    });

                var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
                embed.AddField($"Page {page}/{totalPages}", string.Join("\n", lines));
            }

            // Pagination buttons
            var components = new System.Collections.Generic.List<DiscordComponent>();
            if (page > 1)
            {
                components.Add(new DiscordButtonComponent(ButtonStyle.Secondary, $"bal_history_{page - 1}", "⏮ Prev"));
            }
            if (txs != null && txs.Count == pageSize && page * pageSize < totalCount)
            {
                components.Add(new DiscordButtonComponent(ButtonStyle.Secondary, $"bal_history_{page + 1}", "Next ⏭"));
            }

            var builder = new DiscordInteractionResponseBuilder()
                .AddEmbed(embed);

            if (components.Count > 0)
            {
                builder.AddComponents(components);
            }

            // Send a new message for history instead of editing the original
            await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, builder);
        }

        private static async Task HandleDepositInfoAsync(ComponentInteractionCreateEventArgs e)
        {
            var embed = new DiscordEmbedBuilder()
                .WithTitle("How to Deposit")
                .WithDescription("Use `!d <amount>` or `!deposit <amount>` to create a deposit request.\n\nExamples:\n`!d 100` → deposit 100M\n`!d 0.5` → deposit 0.5M")
                .WithColor(DiscordColor.Gold)
                .WithTimestamp(DateTimeOffset.UtcNow);

            var builder = new DiscordInteractionResponseBuilder()
                .AddEmbed(embed)
                .AsEphemeral(true);

            await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, builder);
        }

        private static async Task HandleWithdrawInfoAsync(ComponentInteractionCreateEventArgs e)
        {
            var embed = new DiscordEmbedBuilder()
                .WithTitle("How to Withdraw")
                .WithDescription("Use `!w <amount>` or `!withdraw <amount>` to create a withdrawal request.\n\nExamples:\n`!w 100` → withdraw 100M\n`!w 0.5` → withdraw 0.5M")
                .WithColor(DiscordColor.Gold)
                .WithTimestamp(DateTimeOffset.UtcNow);

            var builder = new DiscordInteractionResponseBuilder()
                .AddEmbed(embed)
                .AsEphemeral(true);

            await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, builder);
        }
    }
}
