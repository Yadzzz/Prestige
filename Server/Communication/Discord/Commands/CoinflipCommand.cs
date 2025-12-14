using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Server.Client.Coinflips;
using Server.Client.Users;
using Server.Client.Utils;
using Server.Infrastructure;
using Server.Infrastructure.Discord;

namespace Server.Communication.Discord.Commands
{
    public class CoinflipCommand : BaseCommandModule
    {
        private static readonly TimeSpan RateLimitInterval = TimeSpan.FromSeconds(1);

        [Command("coinflip")]
        [Aliases("cf")]
        public async Task Coinflip(CommandContext ctx, string amount = null)
        {
            if (!await DiscordChannelPermissionService.EnforceCoinflipChannelAsync(ctx))
            {
                return;
            }

            if (RateLimiter.IsRateLimited(ctx.User.Id, "coinflip", RateLimitInterval))
            {
                await ctx.RespondAsync("You're doing that too fast. Please wait a moment.");
                return;
            }

            var env = ServerEnvironment.GetServerEnvironment();
            var serverManager = env.ServerManager;
            var usersService = serverManager.UsersService;
            var coinflipsService = serverManager.CoinflipsService;

            var user = await usersService.EnsureUserAsync(ctx.User.Id.ToString(), ctx.User.Username, ctx.Member.DisplayName);
            if (user == null)
                return;

            long amountK;

            if (string.IsNullOrWhiteSpace(amount))
            {
                // No amount specified -> all-in
                amountK = user.Balance;
            }
            else if (!GpParser.TryParseAmountInK(amount, out amountK, out var error))
            {
                await ctx.RespondAsync($"Invalid amount: {error}\nExamples: `!coinflip 100`, `!cf 0.5`, `!cf 1b`, `!cf 1000m`, or `!coinflip` for all-in.");
                return;
            }

            if (amountK < GpFormatter.MinimumBetAmountK)
            {
                await ctx.RespondAsync($"Minimum bet is {GpFormatter.Format(GpFormatter.MinimumBetAmountK)}.");
                return;
            }

            if (user.Balance < amountK)
            {
                await ctx.RespondAsync("You don't have enough balance for this flip.");
                return;
            }

            if (!await usersService.RemoveBalanceAsync(user.Identifier, amountK))
            {
                await ctx.RespondAsync("Failed to lock balance for this flip. Please try again.");
                return;
            }

            var flip = await coinflipsService.CreateCoinflipAsync(user, amountK);
            if (flip == null)
            {
                await usersService.AddBalanceAsync(user.Identifier, amountK);
                await ctx.RespondAsync("Failed to create coinflip. Please try again later.");
                return;
            }

            var prettyAmount = GpFormatter.Format(flip.AmountK);

            var embed = new DiscordEmbedBuilder()
                .WithTitle("One, two, three")
                .WithDescription("**We are gonna see...**\n\n*What's it gonna be?*")
                .WithColor(DiscordColor.Gold)
                .WithThumbnail("https://i.imgur.com/W6mx4qd.gif")
                .WithFooter(ServerConfiguration.ServerName)
                .WithTimestamp(DateTimeOffset.UtcNow);

            //var headsButton = new DiscordButtonComponent(DiscordButtonStyle.Success, $"cf_heads_{flip.Id}", "🪙 Heads");
            //var tailsButton = new DiscordButtonComponent(DiscordButtonStyle.Primary, $"cf_tails_{flip.Id}", "🧠 Tails");
            //var exitButton = new DiscordButtonComponent(DiscordButtonStyle.Danger, $"cf_exit_{flip.Id}", "Exit");

            var headsButton = new DiscordButtonComponent(
                DiscordButtonStyle.Secondary,
                $"cf_heads_{flip.Id}",
                " ",
                emoji: new DiscordComponentEmoji(DiscordIds.CoinflipHeadsEmojiId)
            );

            var tailsButton = new DiscordButtonComponent(
                DiscordButtonStyle.Secondary,
                $"cf_tails_{flip.Id}",
                " ",
                emoji: new DiscordComponentEmoji(DiscordIds.CoinflipTailsEmojiId)
            );

            var exitButton = new DiscordButtonComponent(
                DiscordButtonStyle.Secondary,
                $"cf_exit_{flip.Id}",
                "Refund",
                emoji: new DiscordComponentEmoji(DiscordIds.CoinflipExitEmojiId)
            );

            var message = await ctx.RespondAsync(new DiscordMessageBuilder()
                .AddEmbed(embed)
                .AddActionRowComponent(new[] { headsButton, tailsButton, exitButton }));

            // Only update if the status is still Pending.
            // If the user already clicked a button (race condition), the status might be Finished or Cancelled.
            // In that case, we do NOT want to overwrite it back to Pending.
            await coinflipsService.UpdateCoinflipOutcomeAsync(flip.Id, choseHeads: false, resultHeads: false, status: CoinflipStatus.Pending, messageId: message.Id, channelId: message.Channel.Id, expectedStatus: CoinflipStatus.Pending);
        }
    }
}
