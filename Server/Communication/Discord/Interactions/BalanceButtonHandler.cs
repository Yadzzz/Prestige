using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Server.Client.Transactions;
using Server.Client.Users;
using Server.Client.Utils;
using Server.Infrastructure.Discord;
using Server.Communication.Discord.Commands;

namespace Server.Communication.Discord.Interactions
{
    public static class BalanceButtonHandler
    {
        public static async Task Handle(DiscordClient client, ComponentInteractionCreatedEventArgs e)
        {
            if (RateLimiter.IsRateLimited(e.User.Id))
            {
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("You're doing that too fast.").AsEphemeral(true));
                return;
            }

            // All balance buttons are user-side; only the invoker may use them
            var identifier = e.User?.Id.ToString();
            if (string.IsNullOrEmpty(identifier))
                return;

            // Check ownership if the ID contains the user ID
            // Format: bal_{action}_{userId}_{args}
            var parts = e.Id.Split('_');
            if (parts.Length >= 3)
            {
                var ownerId = parts[2];
                if (!string.Equals(ownerId, identifier, StringComparison.OrdinalIgnoreCase))
                {
                    await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource,
                        new DiscordInteractionResponseBuilder().WithContent("This is not your balance menu.").AsEphemeral(true));
                    return;
                }
            }

            if (e.Id.StartsWith("bal_history", StringComparison.OrdinalIgnoreCase))
            {
                await HandleHistoryAsync(e);
                return;
            }

            if (e.Id.StartsWith("bal_wallet", StringComparison.OrdinalIgnoreCase))
            {
                await HandleWalletAsync(e);
                return;
            }

            if (e.Id.StartsWith("bal_deposit", StringComparison.OrdinalIgnoreCase))
            {
                await HandleDepositInfoAsync(e);
                return;
            }

            if (e.Id.StartsWith("bal_withdraw", StringComparison.OrdinalIgnoreCase))
            {
                await HandleWithdrawInfoAsync(e);
                return;
            }

            if (e.Id.StartsWith("bal_buy", StringComparison.OrdinalIgnoreCase))
            {
                // Placeholder: do nothing for now
                await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.DeferredMessageUpdate);
                return;
            }
        }

        private static async Task HandleWalletAsync(ComponentInteractionCreatedEventArgs e)
        {
            var env = ServerEnvironment.GetServerEnvironment();
            var usersService = env.ServerManager.UsersService;

            var user = await usersService.EnsureUserAsync(e.User.Id.ToString(), e.User.Username, e.User.Username);
            if (user == null)
                return;

            var formatted = GpFormatter.Format(user.Balance);

            // Build embed
            var embed = new DiscordEmbedBuilder()
                .WithTitle("Balance")
                .WithDescription($"{e.User.Username}, you have `{formatted}`.")
                .WithColor(DiscordColor.Gold)
                .WithThumbnail("https://i.imgur.com/DHXgtn5.gif")
                .WithFooter(ServerConfiguration.ServerName)
                .WithTimestamp(System.DateTimeOffset.UtcNow);

            // Buttons row 1
            var row1 = new[]
            {
                new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"bal_buy_{e.User.Id}", "Buy", emoji: new DiscordComponentEmoji(DiscordIds.BuyEmojiId)),
                new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"bal_deposit_{e.User.Id}", "Deposit", emoji: new DiscordComponentEmoji(DiscordIds.DepositEmojiId)),
                new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"bal_withdraw_{e.User.Id}", " ", emoji: new DiscordComponentEmoji(DiscordIds.WithdrawEmojiId)),
                new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"bal_history_{e.User.Id}", " ", emoji: new DiscordComponentEmoji(DiscordIds.BalanceSheetEmojiId)),
            };

            var builder = new DiscordInteractionResponseBuilder()
                .AddEmbed(embed)
                .AddComponents(row1);

            await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage, builder);
        }

        private static async Task HandleHistoryAsync(ComponentInteractionCreatedEventArgs e)
        {
            var env = ServerEnvironment.GetServerEnvironment();
            var balanceAdjustmentsService = env.ServerManager.BalanceAdjustmentsService;
            var usersService = env.ServerManager.UsersService;

            var identifier = e.User.Id.ToString();

            // Parse page from custom id: bal_history_{userId} or bal_history_{userId}_{page}
            var page = 1;
            var idParts = e.Id.Split('_');
            // bal, history, userId, page
            if (idParts.Length == 4 && int.TryParse(idParts[3], out var parsedPage) && parsedPage > 0)
            {
                page = parsedPage;
            }

            const int pageSize = 10;

            var user = await usersService.GetUserAsync(identifier);
            var (adjustments, totalCount) = await balanceAdjustmentsService.GetAdjustmentsPageForUserAsync(identifier, page, pageSize);

            var embed = new DiscordEmbedBuilder()
                .WithTitle("Transaction History")
                .WithColor(DiscordColor.Blurple)
                //.WithThumbnail("https://i.imgur.com/DHXgtn5.gif")
                .WithThumbnail("https://i.imgur.com/Axcs6YE.gif")
                .WithTimestamp(DateTimeOffset.UtcNow);

            if (user != null)
            {
                var balanceText = GpFormatter.Format(user.Balance);
                embed.WithDescription($"{e.User.Username}, your current balance is `{balanceText}`.");
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
            var components = new System.Collections.Generic.List<DiscordComponent>();
            if (page > 1)
            {
                components.Add(new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"bal_history_{identifier}_{page - 1}", "⏮ Prev"));
            }
            
            // Add Wallet button in the middle
            components.Add(new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"bal_wallet_{identifier}", "Wallet", emoji: new DiscordComponentEmoji(DiscordIds.WalletEmojiId)));

            if (adjustments != null && adjustments.Count == pageSize && page * pageSize < totalCount)
            {
                components.Add(new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"bal_history_{identifier}_{page + 1}", "Next ⏭"));
            }

            var builder = new DiscordInteractionResponseBuilder()
                .AddEmbed(embed);

            if (components.Count > 0)
            {
                builder.AddComponents(components);
            }

            // Update the message instead of sending a new one
            await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.UpdateMessage, builder);
        }

        private static async Task HandleDepositInfoAsync(ComponentInteractionCreatedEventArgs e)
        {
            var embed = new DiscordEmbedBuilder()
                .WithTitle("How to Deposit")
                .WithDescription("Use `!d <amount>` or `!deposit <amount>` to create a deposit request.\n\nExamples:\n`!d 100` → deposit 100M\n`!d 0.5` → deposit 0.5M")
                .WithColor(DiscordColor.Gold)
                .WithTimestamp(DateTimeOffset.UtcNow);

            var builder = new DiscordInteractionResponseBuilder()
                .AddEmbed(embed)
                .AsEphemeral(true);

            await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, builder);
        }

        private static async Task HandleWithdrawInfoAsync(ComponentInteractionCreatedEventArgs e)
        {
            var embed = new DiscordEmbedBuilder()
                .WithTitle("How to Withdraw")
                .WithDescription("Use `!w <amount>` or `!withdraw <amount>` to create a withdrawal request.\n\nExamples:\n`!w 100` → withdraw 100M\n`!w 0.5` → withdraw 0.5M")
                .WithColor(DiscordColor.Gold)
                .WithTimestamp(DateTimeOffset.UtcNow);

            var builder = new DiscordInteractionResponseBuilder()
                .AddEmbed(embed)
                .AsEphemeral(true);

            await e.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, builder);
        }
    }
}
