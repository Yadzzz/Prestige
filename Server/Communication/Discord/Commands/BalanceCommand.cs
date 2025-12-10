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
        public async Task Balance(CommandContext ctx, DiscordMember? member = null)
        {
            if (RateLimiter.IsRateLimited(ctx.User.Id, "balance", RateLimitInterval))
            {
                await ctx.RespondAsync("You're doing that too fast. Please wait a moment.");
                return;
            }

            // Determine target
            DiscordUser targetUser = member ?? ctx.User;

            bool isSelf = targetUser.Id == ctx.User.Id;

            // If checking someone else, enforce permissions
            if (!isSelf)
            {
                if (ctx.Member == null)
                {
                    await ctx.RespondAsync("You cannot check other users' balances in DMs.");
                    return;
                }

                if (!ctx.Member.IsStaff())
                    return;
            }

            var env = ServerEnvironment.GetServerEnvironment();
            var usersService = env.ServerManager.UsersService;

            string displayName = targetUser.Username;
            if (targetUser is DiscordMember m)
            {
                displayName = m.DisplayName;
            }
            else if (isSelf && ctx.Member != null)
            {
                displayName = ctx.Member.DisplayName;
            }

            var user = await usersService.EnsureUserAsync(targetUser.Id.ToString(), targetUser.Username, displayName);
            if (user == null)
            {
                if (!isSelf) await ctx.RespondAsync("Failed to resolve target user.");
                return;
            }

            var formatted = GpFormatter.Format(user.Balance);

            if (isSelf)
            {
                // Self View
                var embed = new DiscordEmbedBuilder()
                    .WithTitle("Balance")
                    .WithDescription($"{displayName}, you have `{formatted}`.")
                    .WithColor(DiscordColor.Gold)
                    .WithThumbnail("https://i.imgur.com/DHXgtn5.gif")
                    .WithFooter(ServerConfiguration.ServerName)
                    .WithTimestamp(DateTimeOffset.UtcNow);

                var row1 = new[]
                {
                    new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"bal_buy_{ctx.User.Id}", "Buy", emoji: new DiscordComponentEmoji(DiscordIds.BuyEmojiId)),
                    new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"bal_deposit_{ctx.User.Id}", "Deposit", emoji: new DiscordComponentEmoji(DiscordIds.DepositEmojiId)),
                    new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"bal_withdraw_{ctx.User.Id}", " ", emoji: new DiscordComponentEmoji(DiscordIds.WithdrawEmojiId)),
                    new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"bal_history_{ctx.User.Id}", " ", emoji: new DiscordComponentEmoji(DiscordIds.BalanceSheetEmojiId)),
                };

                await ctx.RespondAsync(new DiscordMessageBuilder()
                    .AddEmbed(embed)
                    .AddComponents(row1));
            }
            else
            {
                // Staff View
                var embed = new DiscordEmbedBuilder()
                    .WithTitle("User Balance")
                    .WithDescription($"{displayName} has **{formatted}** in their wallet.")
                    .WithColor(DiscordColor.Blurple)
                    .WithThumbnail(targetUser.AvatarUrl ?? targetUser.DefaultAvatarUrl)
                    .WithFooter(ServerConfiguration.ServerName)
                    .WithTimestamp(DateTimeOffset.UtcNow);

                await ctx.RespondAsync(new DiscordMessageBuilder()
                    .WithContent(targetUser.Mention)
                    .AddEmbed(embed));
            }
        }
    }
}
