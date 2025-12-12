using System;
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
    public static class BalanceButtonHandler
    {
        public static async Task Handle(DiscordSocketClient client, SocketMessageComponent component)
        {
            if (RateLimiter.IsRateLimited(component.User.Id))
            {
                await component.RespondAsync("You're doing that too fast.", ephemeral: true);
                return;
            }

            // All balance buttons are user-side; only the invoker may use them
            var identifier = component.User?.Id.ToString();
            if (string.IsNullOrEmpty(identifier))
                return;

            // Check ownership if the ID contains the user ID
            // Format: bal_{action}_{userId}_{args}
            var parts = component.Data.CustomId.Split('_');
            if (parts.Length >= 3)
            {
                var ownerId = parts[2];
                if (!string.Equals(ownerId, identifier, StringComparison.OrdinalIgnoreCase))
                {
                    await component.RespondAsync("This is not your balance menu.", ephemeral: true);
                    return;
                }
            }

            if (component.Data.CustomId.StartsWith("bal_history", StringComparison.OrdinalIgnoreCase))
            {
                await HandleHistoryAsync(component);
                return;
            }

            if (component.Data.CustomId.StartsWith("bal_wallet", StringComparison.OrdinalIgnoreCase))
            {
                await HandleWalletAsync(component);
                return;
            }

            if (component.Data.CustomId.StartsWith("bal_deposit", StringComparison.OrdinalIgnoreCase))
            {
                await HandleDepositInfoAsync(component);
                return;
            }

            if (component.Data.CustomId.StartsWith("bal_withdraw", StringComparison.OrdinalIgnoreCase))
            {
                await HandleWithdrawInfoAsync(component);
                return;
            }

            if (component.Data.CustomId.StartsWith("bal_buy", StringComparison.OrdinalIgnoreCase))
            {
                // Placeholder: do nothing for now
                await component.DeferAsync();
                return;
            }
        }

        private static async Task HandleWalletAsync(SocketMessageComponent component)
        {
            var env = ServerEnvironment.GetServerEnvironment();
            var usersService = env.ServerManager.UsersService;

            var user = await usersService.EnsureUserAsync(component.User.Id.ToString(), component.User.Username, component.User.Username);
            if (user == null)
                return;

            var formatted = GpFormatter.Format(user.Balance);

            // Build embed
            var embed = new EmbedBuilder()
                .WithTitle("Balance")
                .WithDescription($"{component.User.Username}, you have `{formatted}`.")
                .WithColor(Color.Gold)
                .WithThumbnailUrl("https://i.imgur.com/DHXgtn5.gif")
                .WithFooter(ServerConfiguration.ServerName)
                .WithCurrentTimestamp();

            // Buttons row 1
            var builder = new ComponentBuilder()
                .WithButton("Buy", $"bal_buy_{component.User.Id}", ButtonStyle.Secondary, new Emote(DiscordIds.BuyEmojiId, "buy", false))
                .WithButton("Deposit", $"bal_deposit_{component.User.Id}", ButtonStyle.Secondary, new Emote(DiscordIds.DepositEmojiId, "deposit", false))
                .WithButton(" ", $"bal_withdraw_{component.User.Id}", ButtonStyle.Secondary, new Emote(DiscordIds.WithdrawEmojiId, "withdraw", false))
                .WithButton(" ", $"bal_history_{component.User.Id}", ButtonStyle.Secondary, new Emote(DiscordIds.BalanceSheetEmojiId, "history", false));

            await component.UpdateAsync(msg =>
            {
                msg.Embed = embed.Build();
                msg.Components = builder.Build();
            });
        }

        private static async Task HandleHistoryAsync(SocketMessageComponent component)
        {
            var env = ServerEnvironment.GetServerEnvironment();
            var balanceAdjustmentsService = env.ServerManager.BalanceAdjustmentsService;
            var usersService = env.ServerManager.UsersService;

            var identifier = component.User.Id.ToString();

            // Parse page from custom id: bal_history_{userId} or bal_history_{userId}_{page}
            var page = 1;
            var idParts = component.Data.CustomId.Split('_');
            // bal, history, userId, page
            if (idParts.Length == 4 && int.TryParse(idParts[3], out var parsedPage) && parsedPage > 0)
            {
                page = parsedPage;
            }

            const int pageSize = 10;

            var user = await usersService.GetUserAsync(identifier);
            var (adjustments, totalCount) = await balanceAdjustmentsService.GetAdjustmentsPageForUserAsync(identifier, page, pageSize);

            var embed = new EmbedBuilder()
                .WithTitle("Transaction History")
                .WithColor(Color.Blue)
                //.WithThumbnailUrl("https://i.imgur.com/DHXgtn5.gif")
                .WithThumbnailUrl("https://i.imgur.com/Axcs6YE.gif")
                .WithCurrentTimestamp();

            if (user != null)
            {
                var balanceText = GpFormatter.Format(user.Balance);
                embed.WithDescription($"{component.User.Username}, your current balance is `{balanceText}`.");
            }

            if (adjustments == null || adjustments.Count == 0)
            {
                embed.AddField("History", "No transactions found yet.");
            }
            else
            {
                var lines = adjustments
                    .OrderByDescending(a => a.Id)
                    .Select(a =>
                    {
                        var typeLabel = a.AdjustmentType.ToString();
                        // Simplify labels for display
                        if (a.AdjustmentType == BalanceAdjustmentType.AdminAdd) typeLabel = "Added";
                        else if (a.AdjustmentType == BalanceAdjustmentType.AdminGift) typeLabel = "Gift";
                        else if (a.AdjustmentType == BalanceAdjustmentType.AdminRemove) typeLabel = "Removed";
                        else if (a.AdjustmentType == BalanceAdjustmentType.Deposit) typeLabel = "Deposit";
                        else if (a.AdjustmentType == BalanceAdjustmentType.Withdraw) typeLabel = "Withdraw";

                        var amount = GpFormatter.Format(a.AmountK);
                        var when = a.CreatedAt.ToString("yyyy-MM-dd HH:mm");
                        return $"`#{a.Id}` {typeLabel} **{amount}** • {when}";
                    });

                var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
                embed.AddField($"Page {page}/{totalPages}", string.Join("\n", lines));
            }

            // Pagination buttons
            var builder = new ComponentBuilder();
            if (page > 1)
            {
                builder.WithButton("⏮ Prev", $"bal_history_{identifier}_{page - 1}", ButtonStyle.Secondary);
            }
            
            // Add Wallet button in the middle
            builder.WithButton("Wallet", $"bal_wallet_{identifier}", ButtonStyle.Secondary, new Emote(DiscordIds.WalletEmojiId, "wallet", false));

            if (adjustments != null && adjustments.Count == pageSize && page * pageSize < totalCount)
            {
                builder.WithButton("Next ⏭", $"bal_history_{identifier}_{page + 1}", ButtonStyle.Secondary);
            }

            // Update the message instead of sending a new one
            await component.UpdateAsync(msg =>
            {
                msg.Embed = embed.Build();
                msg.Components = builder.Build();
            });
        }

        private static async Task HandleDepositInfoAsync(SocketMessageComponent component)
        {
            var embed = new EmbedBuilder()
                .WithTitle("How to Deposit")
                .WithDescription("Use `!d <amount>` or `!deposit <amount>` to create a deposit request.\n\nExamples:\n`!d 100` → deposit 100M\n`!d 0.5` → deposit 0.5M")
                .WithColor(Color.Gold)
                .WithCurrentTimestamp();

            await component.RespondAsync(embed: embed.Build(), ephemeral: true);
        }

        private static async Task HandleWithdrawInfoAsync(SocketMessageComponent component)
        {
            var embed = new EmbedBuilder()
                .WithTitle("How to Withdraw")
                .WithDescription("Use `!w <amount>` or `!withdraw <amount>` to create a withdrawal request.\n\nExamples:\n`!w 100` → withdraw 100M\n`!w 0.5` → withdraw 0.5M")
                .WithColor(Color.Gold)
                .WithCurrentTimestamp();

            await component.RespondAsync(embed: embed.Build(), ephemeral: true);
        }
    }
}
