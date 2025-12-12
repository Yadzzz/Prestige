using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Server.Client.Coinflips;
using Server.Client.Users;
using Server.Client.Utils;
using Server.Infrastructure;
using Server.Infrastructure.Discord;

namespace Server.Communication.Discord.Commands
{
    public class CoinflipCommand : ModuleBase<SocketCommandContext>
    {
        private static readonly TimeSpan RateLimitInterval = TimeSpan.FromSeconds(1);

        [Command("coinflip")]
        [Alias("cf")]
        public async Task Coinflip(string amount = null)
        {
            if (!await DiscordChannelPermissionService.EnforceCoinflipChannelAsync(Context))
            {
                return;
            }

            if (RateLimiter.IsRateLimited(Context.User.Id, "coinflip", RateLimitInterval))
            {
                await ReplyAsync("You're doing that too fast. Please wait a moment.");
                return;
            }

            var env = ServerEnvironment.GetServerEnvironment();
            var serverManager = env.ServerManager;
            var usersService = serverManager.UsersService;
            var coinflipsService = serverManager.CoinflipsService;

            var user = await usersService.EnsureUserAsync(Context.User.Id.ToString(), Context.User.Username, (Context.User as SocketGuildUser)?.DisplayName ?? Context.User.Username);
            if (user == null)
                return;

            long amountK;

            if (string.IsNullOrWhiteSpace(amount))
            {
                // No amount specified -> all-in
                amountK = user.Balance;
            }
            else if (!GpParser.TryParseAmountInK(amount, out amountK))
            {
                await ReplyAsync("Invalid amount. Examples: `!coinflip 100`, `!cf 0.5`, `!cf 1b`, `!cf 1000m`, or `!coinflip` for all-in.");
                return;
            }

            if (amountK < GpFormatter.MinimumBetAmountK)
            {
                await ReplyAsync($"Minimum bet is {GpFormatter.Format(GpFormatter.MinimumBetAmountK)}.");
                return;
            }

            if (user.Balance < amountK)
            {
                await ReplyAsync("You don't have enough balance for this flip.");
                return;
            }

            if (!await usersService.RemoveBalanceAsync(user.Identifier, amountK))
            {
                await ReplyAsync("Failed to lock balance for this flip. Please try again.");
                return;
            }

            var flip = await coinflipsService.CreateCoinflipAsync(user, amountK);
            if (flip == null)
            {
                await usersService.AddBalanceAsync(user.Identifier, amountK);
                await ReplyAsync("Failed to create coinflip. Please try again later.");
                return;
            }

            var prettyAmount = GpFormatter.Format(flip.AmountK);

            var embed = new EmbedBuilder()
                .WithTitle("One, two, three")
                .WithDescription("**We are gonna see...**\n\n*What's it gonna be?*")
                .WithColor(Color.Gold)
                .WithThumbnailUrl("https://i.imgur.com/W6mx4qd.gif")
                .WithFooter(ServerConfiguration.ServerName)
                .WithCurrentTimestamp();

            var builder = new ComponentBuilder()
                .WithButton(" ", $"cf_heads_{flip.Id}", ButtonStyle.Secondary, new Emote(DiscordIds.CoinflipHeadsEmojiId, "heads", false))
                .WithButton(" ", $"cf_tails_{flip.Id}", ButtonStyle.Secondary, new Emote(DiscordIds.CoinflipTailsEmojiId, "tails", false))
                .WithButton("Refund", $"cf_exit_{flip.Id}", ButtonStyle.Secondary, new Emote(DiscordIds.CoinflipExitEmojiId, "exit", false));

            try
            {
                var message = await ReplyAsync(embed: embed.Build(), components: builder.Build());
                await coinflipsService.UpdateCoinflipOutcomeAsync(flip.Id, choseHeads: false, resultHeads: false, status: CoinflipStatus.Pending, messageId: message.Id, channelId: message.Channel.Id);
            }
            catch (Exception ex)
            {
                env.ServerManager.LoggerManager.LogError($"[CoinflipCommand] Failed to send message: {ex}");

                // Refund and cancel to prevent ghost flips
                await usersService.AddBalanceAsync(user.Identifier, amountK);
                await coinflipsService.UpdateCoinflipOutcomeAsync(flip.Id, false, false, CoinflipStatus.Cancelled, 0, 0);

                try
                {
                    await ReplyAsync("Failed to start coinflip. Your bet has been refunded.");
                }
                catch { /* Ignore if we can't reply */ }
            }
        }
    }
}
