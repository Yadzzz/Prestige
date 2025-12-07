using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Server.Client.Users;
using Server.Client.Transactions;
using Server.Infrastructure;
using Server.Client.Utils;
using Server.Communication.Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Server.Communication.Discord.Commands
{
    public class BalanceCommand : BaseCommandModule
    {
        private static readonly TimeSpan RateLimitInterval = TimeSpan.FromSeconds(1);
        [Command("balance")]
        [Aliases("bal", "wallet", "money", "gp", "b")]
        public async Task Balance(CommandContext ctx)
        {
            if (RateLimiter.IsRateLimited(ctx.User.Id, "balance", RateLimitInterval))
            {
                await ctx.RespondAsync("You're doing that too fast. Please wait a moment.");
                return;
            }

            var env = ServerEnvironment.GetServerEnvironment();
            var usersService = env.ServerManager.UsersService;
            var transactionsService = env.ServerManager.TransactionsService;
            var adjustmentsService = env.ServerManager.BalanceAdjustmentsService;

            var user = await usersService.EnsureUserAsync(ctx.User.Id.ToString(), ctx.User.Username, ctx.Member.DisplayName);
            if (user == null)
                return;

            var formatted = GpFormatter.Format(user.Balance);

            // Build embed
            var embed = new DiscordEmbedBuilder()
                .WithTitle("Balance")
                .WithDescription($"{ctx.Member.DisplayName}, you have **{formatted}** in your wallet.")
                .WithColor(DiscordColor.Gold)
                .WithThumbnail("https://i.imgur.com/DHXgtn5.gif")
                .WithFooter($"Prestige Bets")
                //.WithFooter($"Prestige Bets • {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm}");
                .WithTimestamp(System.DateTimeOffset.UtcNow);

            // Buttons row 1
            var row1 = new[]
            {
            new DiscordButtonComponent(ButtonStyle.Primary, "bal_deposit", "Deposit", emoji: new DiscordComponentEmoji("💳")),
            new DiscordButtonComponent(ButtonStyle.Secondary, "bal_history", "History", emoji: new DiscordComponentEmoji("📜")),
            new DiscordButtonComponent(ButtonStyle.Primary, "bal_withdraw", "Withdraw", emoji: new DiscordComponentEmoji("🏧")),
            //new DiscordButtonComponent(ButtonStyle.Success, "bal_buy", "Buy", emoji: new DiscordComponentEmoji("🛒"))
        };

            // Buttons row 2
        //    var row2 = new[]
        //    {
        //    new DiscordButtonComponent(ButtonStyle.Secondary, "bal_profile", "Profile", emoji: new DiscordComponentEmoji("👤")),
        //    new DiscordButtonComponent(ButtonStyle.Secondary, "bal_history", "History", emoji: new DiscordComponentEmoji("📜")),
        //    new DiscordButtonComponent(ButtonStyle.Secondary, "bal_games", "Games", emoji: new DiscordComponentEmoji("🎲"))
        //};

            // Send embed + buttons
            await ctx.RespondAsync(new DiscordMessageBuilder()
                .AddEmbed(embed)
                .AddComponents(row1)
                //.AddComponents(row2)
            );
        }

        [Command("b")]
        [RequireRoles(RoleCheckMode.Any, "Staff", "Admin", "Moderator")]
        public async Task Balance(CommandContext ctx, DiscordMember member)
        {
            var env = ServerEnvironment.GetServerEnvironment();
            var usersService = env.ServerManager.UsersService;

            var user = await usersService.EnsureUserAsync(member.Id.ToString(), member.Username, member.DisplayName);
            if (user == null)
            {
                await ctx.RespondAsync("Failed to resolve target user.");
                return;
            }

            var formatted = GpFormatter.Format(user.Balance);
            var staffName = ctx.Member?.DisplayName ?? ctx.User.Username;

            var embed = new DiscordEmbedBuilder()
                .WithTitle("User Balance")
                .WithDescription($"{member.DisplayName} has **{formatted}** in their wallet.")
                .WithColor(DiscordColor.Blurple)
                .WithThumbnail(member.AvatarUrl ?? member.DefaultAvatarUrl)
                .WithFooter("Prestige Bets")
                .WithTimestamp(DateTimeOffset.UtcNow);

            await ctx.RespondAsync(new DiscordMessageBuilder()
                .WithContent(member.Mention)
                .AddEmbed(embed));
        }

        [Command("history")]
        [Aliases("transactions", "tx", "txs")]
        public async Task History(CommandContext ctx, int page = 1)
        {
            if (page < 1)
            {
                page = 1;
            }

            var env = ServerEnvironment.GetServerEnvironment();
            var usersService = env.ServerManager.UsersService;
            var transactionsService = env.ServerManager.TransactionsService;

            var identifier = ctx.User.Id.ToString();

            const int pageSize = 10;

            usersService.TryGetUser(identifier, out var user);
            var txs = transactionsService.GetTransactionsPageForUser(identifier, page, pageSize, out var totalTxCount);
            var adjustments = adjustmentsService.GetAdminAddAdjustmentsForUser(identifier, page, pageSize, out var totalAdjCount);

            var totalCount = totalTxCount + totalAdjCount;

            var (embed, components) = HistoryViewBuilder.BuildHistoryView(
                discordUserName: ctx.User.Username,
                user,
                txs,
                adjustments,
                page,
                pageSize,
                totalCount,
                historyCustomIdPrefix: "bal_history");

            var builder = new DiscordMessageBuilder()
                .AddEmbed(embed);

            if (components != null && components.Count > 0)
            {
                builder.AddComponents(components);
            }

            await ctx.RespondAsync(builder);
        }
    }
}
