using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Server.Client.Users;
using Server.Client.Transactions;
using Server.Infrastructure;
using Server.Client.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using Server.Infrastructure.Discord;

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

            var user = await usersService.EnsureUserAsync(ctx.User.Id.ToString(), ctx.User.Username, ctx.Member.DisplayName);
            if (user == null)
                return;

            var formatted = GpFormatter.Format(user.Balance);

            // Build embed
            var embed = new DiscordEmbedBuilder()
                .WithTitle("Balance")
                .WithDescription($"{ctx.Member.DisplayName}, you have `{formatted}`.")
                .WithColor(DiscordColor.Gold)
                .WithThumbnail("https://i.imgur.com/DHXgtn5.gif")
                .WithFooter(ServerConfiguration.ServerName)
                //.WithFooter($"Prestige Bets • {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm}");
                .WithTimestamp(System.DateTimeOffset.UtcNow);

            // Buttons row 1
            var row1 = new[]
            {
                new DiscordButtonComponent(ButtonStyle.Secondary, $"bal_buy_{ctx.User.Id}", "Buy", emoji: new DiscordComponentEmoji(DiscordIds.BuyEmojiId)),
                new DiscordButtonComponent(ButtonStyle.Secondary, $"bal_deposit_{ctx.User.Id}", "Deposit", emoji: new DiscordComponentEmoji(DiscordIds.DepositEmojiId)),
                new DiscordButtonComponent(ButtonStyle.Secondary, $"bal_withdraw_{ctx.User.Id}", " ", emoji: new DiscordComponentEmoji(DiscordIds.WithdrawEmojiId)),
                new DiscordButtonComponent(ButtonStyle.Secondary, $"bal_history_{ctx.User.Id}", " ", emoji: new DiscordComponentEmoji(DiscordIds.BalanceSheetEmojiId)),
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

        [Command("balance")]
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
                .WithFooter(ServerConfiguration.ServerName)
                .WithTimestamp(DateTimeOffset.UtcNow);

            await ctx.RespondAsync(new DiscordMessageBuilder()
                .WithContent(member.Mention)
                .AddEmbed(embed));
        }
    }
}
