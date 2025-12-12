using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Server.Client.Users;
using Server.Client.Transactions;
using Server.Infrastructure;
using Server.Client.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using Server.Infrastructure.Discord;
using System.Threading.Tasks;

namespace Server.Communication.Discord.Commands
{
    public class BalanceCommand : ModuleBase<SocketCommandContext>
    {
        private static readonly TimeSpan RateLimitInterval = TimeSpan.FromSeconds(1);

        [Command("balance")]
        [Alias("bal", "wallet", "money", "gp", "b")]
        public async Task Balance(SocketGuildUser? member = null)
        {
            if (RateLimiter.IsRateLimited(Context.User.Id, "balance", RateLimitInterval))
            {
                await ReplyAsync("You're doing that too fast. Please wait a moment.");
                return;
            }

            // Determine target
            SocketUser targetUser = member ?? Context.User;

            bool isSelf = targetUser.Id == Context.User.Id;

            // If checking someone else, enforce permissions
            if (!isSelf)
            {
                if (Context.Guild == null)
                {
                    await ReplyAsync("You cannot check other users' balances in DMs.");
                    return;
                }

                if (Context.User is SocketGuildUser guildUser && !guildUser.IsStaff())
                    return;
            }

            var env = ServerEnvironment.GetServerEnvironment();
            var usersService = env.ServerManager.UsersService;

            string displayName = targetUser.Username;
            if (targetUser is SocketGuildUser m)
            {
                displayName = m.DisplayName;
            }
            else if (isSelf && Context.User is SocketGuildUser selfMember)
            {
                displayName = selfMember.DisplayName;
            }

            var user = await usersService.EnsureUserAsync(targetUser.Id.ToString(), targetUser.Username, displayName);
            if (user == null)
            {
                if (!isSelf) await ReplyAsync("Failed to resolve target user.");
                return;
            }

            var formatted = GpFormatter.Format(user.Balance);

            if (isSelf)
            {
                // Self View
                var embed = new EmbedBuilder()
                    .WithTitle("Balance")
                    .WithDescription($"{displayName}, you have `{formatted}`.")
                    .WithColor(Color.Gold)
                    .WithThumbnailUrl("https://i.imgur.com/DHXgtn5.gif")
                    .WithFooter(ServerConfiguration.ServerName)
                    .WithCurrentTimestamp();

                var builder = new ComponentBuilder()
                    .WithButton("Buy", $"bal_buy_{Context.User.Id}", ButtonStyle.Secondary, emote: new Emote(DiscordIds.BuyEmojiId, "buy", false))
                    .WithButton("Deposit", $"bal_deposit_{Context.User.Id}", ButtonStyle.Secondary, emote: new Emote(DiscordIds.DepositEmojiId, "deposit", false))
                    .WithButton(" ", $"bal_withdraw_{Context.User.Id}", ButtonStyle.Secondary, emote: new Emote(DiscordIds.WithdrawEmojiId, "withdraw", false))
                    .WithButton(" ", $"bal_history_{Context.User.Id}", ButtonStyle.Secondary, emote: new Emote(DiscordIds.BalanceSheetEmojiId, "history", false));

                await ReplyAsync(embed: embed.Build(), components: builder.Build());
            }
            else
            {
                // Staff View
                var embed = new EmbedBuilder()
                    .WithTitle("User Balance")
                    .WithDescription($"{displayName} has **{formatted}** in their wallet.")
                    .WithColor(Color.Blue)
                    .WithThumbnailUrl(targetUser.GetAvatarUrl() ?? targetUser.GetDefaultAvatarUrl())
                    .WithFooter(ServerConfiguration.ServerName)
                    .WithCurrentTimestamp();

                await ReplyAsync(message: targetUser.Mention, embed: embed.Build());
            }
        }
    }
}
