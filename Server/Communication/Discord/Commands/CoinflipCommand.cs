using System;
using System.Globalization;
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
        public async Task Coinflip(CommandContext ctx, string amount)
        {
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

            if (!TryParseAmountInK(amount, out var amountK))
            {
                await ctx.RespondAsync("Invalid amount. Example: `!coinflip 100` or `!cf 0.5`");
                return;
            }

            if (user.Balance < amountK)
            {
                await ctx.RespondAsync("You don't have enough balance for this flip.");
                return;
            }

            if (!usersService.RemoveBalance(user.Identifier, amountK))
            {
                await ctx.RespondAsync("Failed to lock balance for this flip. Please try again.");
                return;
            }

            var flip = coinflipsService.CreateCoinflip(user, amountK);
            if (flip == null)
            {
                usersService.AddBalance(user.Identifier, amountK);
                await ctx.RespondAsync("Failed to create coinflip. Please try again later.");
                return;
            }

            var prettyAmount = GpFormatter.Format(flip.AmountK);

            var embed = new DiscordEmbedBuilder()
                .WithTitle("One, two, three")
                .WithDescription("We are gonna see...\n\nWhat's it gonna be?")
                .AddField("Bet", prettyAmount, true)
                .WithColor(DiscordColor.Gold)
                .WithThumbnail("https://i.imgur.com/W6mx4qd.gif")
                .WithFooter("Prestige Bets")
                .WithTimestamp(DateTimeOffset.UtcNow);

            //var headsButton = new DiscordButtonComponent(ButtonStyle.Success, $"cf_heads_{flip.Id}", "🪙 Heads");
            //var tailsButton = new DiscordButtonComponent(ButtonStyle.Primary, $"cf_tails_{flip.Id}", "🧠 Tails");
            //var exitButton = new DiscordButtonComponent(ButtonStyle.Danger, $"cf_exit_{flip.Id}", "Exit");

            var headsButton = new DiscordButtonComponent(
                ButtonStyle.Success,
                $"cf_heads_{flip.Id}",
                "Heads",
                emoji: new DiscordComponentEmoji(DiscordIds.CoinflipHeadsEmojiId)
            );

            var tailsButton = new DiscordButtonComponent(
                ButtonStyle.Primary,
                $"cf_tails_{flip.Id}",
                "Tails",
                emoji: new DiscordComponentEmoji(DiscordIds.CoinflipTailsEmojiId)
            );

            var exitButton = new DiscordButtonComponent(
                ButtonStyle.Danger,
                $"cf_exit_{flip.Id}",
                "Exit"
            );

            var message = await ctx.RespondAsync(new DiscordMessageBuilder()
                .AddEmbed(embed)
                .AddComponents(headsButton, tailsButton, exitButton));

            coinflipsService.UpdateCoinflipOutcome(flip.Id, choseHeads: false, resultHeads: false, status: CoinflipStatus.Pending, messageId: message.Id, channelId: message.Channel.Id);
        }

        private bool TryParseAmountInK(string input, out long amountK)
        {
            amountK = 0;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            if (!decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out var amountM))
                return false;

            if (amountM <= 0)
                return false;

            var result = amountM * 1000m;

            amountK = (long)Math.Round(result, MidpointRounding.AwayFromZero);
            return amountK > 0;
        }
    }
}
