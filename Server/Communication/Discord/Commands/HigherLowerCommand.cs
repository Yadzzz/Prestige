using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Server.Client.HigherLower;
using Server.Client.Users;
using Server.Client.Utils;
using Server.Infrastructure;
using Server.Infrastructure.Discord;

namespace Server.Communication.Discord.Commands
{
    public class HigherLowerCommand : BaseCommandModule
    {
        private static readonly TimeSpan RateLimitInterval = TimeSpan.FromSeconds(1);

        [Command("higherlower")]
        [Aliases("hl")]
        public async Task HigherLower(CommandContext ctx, string amount = null)
        {
            // Check if user is staff
            if (!ctx.Member.IsStaff())
            {
                await ctx.RespondAsync("This command is currently restricted to staff members only.");
                return;
            }

            // Assuming we want to enforce channel permissions similar to Blackjack
            // if (!await DiscordChannelPermissionService.EnforceBlackjackChannelAsync(ctx)) return; 

            if (RateLimiter.IsRateLimited(ctx.User.Id, "higherlower", RateLimitInterval))
            {
                await ctx.RespondAsync("You're doing that too fast. Please wait a moment.");
                return;
            }

            if (string.IsNullOrWhiteSpace(amount))
            {
                await ctx.RespondAsync("Please specify an amount. Usage: `!hl <amount>` (e.g. `!hl 100m`).");
                return;
            }

            var env = ServerEnvironment.GetServerEnvironment();
            var serverManager = env.ServerManager;
            var usersService = serverManager.UsersService;
            var hlService = serverManager.HigherLowerService;

            var user = await usersService.EnsureUserAsync(ctx.User.Id.ToString(), ctx.User.Username, ctx.Member.DisplayName);
            if (user == null)
                return;

            long betAmount;

            if (string.IsNullOrWhiteSpace(amount))
            {
                betAmount = user.Balance;
            }
            else if (!GpParser.TryParseAmountInK(amount, out betAmount, out var error))
            {
                await ctx.RespondAsync($"Invalid amount: {error}\nExamples: `!hl 100`, `!hl 0.5`, `!hl 1b`, `!hl 1000m`, or `!hl` for all-in.");
                return;
            }

            if (betAmount < GpFormatter.MinimumBetAmountK)
            {
                await ctx.RespondAsync($"Minimum bet is `{GpFormatter.Format(GpFormatter.MinimumBetAmountK)}`.");
                return;
            }

            if (user.Balance < betAmount)
            {
                await ctx.RespondAsync("You don't have enough balance for this bet.");
                return;
            }

            if (!await usersService.RemoveBalanceAsync(user.Identifier, betAmount))
            {
                await ctx.RespondAsync("Failed to lock balance for this game. Please try again.");
                return;
            }

            // Update local user balance for display
            user.Balance -= betAmount;

            var game = await hlService.CreateGameAsync(user, betAmount);
            if (game == null)
            {
                await usersService.AddBalanceAsync(user.Identifier, betAmount);
                await ctx.RespondAsync("Failed to create Higher/Lower game. Please try again later.");
                return;
            }

            var embed = BuildGameEmbed(game, user, ctx.Client);
            var buttons = BuildButtons(game);

            var builder = new DiscordMessageBuilder().AddEmbed(embed);
            if (buttons.Length > 0)
            {
                builder.AddActionRowComponent(new DiscordActionRowComponent(buttons));
            }

            var message = await ctx.RespondAsync(builder);
            await hlService.UpdateMessageInfoAsync(game.Id, message.Id, message.Channel.Id);
        }

        public static DiscordEmbedBuilder BuildGameEmbed(HigherLowerGame game, User user, DiscordClient client = null)
        {
            var isGameFinished = game.Status != HigherLowerGameStatus.Active;
            var color = DiscordColor.Blue;

            if (game.Status == HigherLowerGameStatus.Won || game.Status == HigherLowerGameStatus.CashedOut)
                color = DiscordColor.Green;
            else if (game.Status == HigherLowerGameStatus.Lost)
                color = DiscordColor.Red;

            var embed = new DiscordEmbedBuilder()
                .WithTitle("Higher / Lower")
                .WithColor(color)
                .WithDescription($"**{user.DisplayName}**'s Game")
                .WithThumbnail("https://i.imgur.com/WRJoody.png")
                .WithFooter($"Round {game.CurrentRound + 1}/{game.MaxRounds} | ID: {game.Id}");

            embed.AddField("Bet", $"`{GpFormatter.Format(game.BetAmount)}`", true);
            embed.AddField("Current Payout", $"`{GpFormatter.Format((long)game.CurrentPayout)}`", true);

            // Card Flow
            var historyEmojis = new List<string>();
            if (game.CardHistory != null && game.CardHistory.Count > 0)
            {
                foreach (var card in game.CardHistory)
                {
                    historyEmojis.Add(card.GetEmoji(client));
                }
            }
            else
            {
                historyEmojis.Add(game.LastCard.GetEmoji(client));
            }

            string cardsDisplay;
            if (historyEmojis.Count > 1)
            {
                // Use a wider separator or invisible character for margin
                // \u3000 is an Ideographic Space (wide space)
                // or just multiple spaces
                var previous = string.Join(" ", historyEmojis.GetRange(0, historyEmojis.Count - 1));
                var current = historyEmojis[historyEmojis.Count - 1];
                
                // Adding a bit of visual separation
                cardsDisplay = $"{previous} \u3000 **-> {current} **";
            }
            else
            {
                cardsDisplay = $"**-> {historyEmojis[0]} **";
            }

            embed.AddField("Cards", cardsDisplay, false);

            if (isGameFinished)
            {
                string resultText = "";
                if (game.Status == HigherLowerGameStatus.Won)
                    resultText = $"You Won `{GpFormatter.Format((long)game.CurrentPayout)}`!";
                else if (game.Status == HigherLowerGameStatus.CashedOut)
                    resultText = $"Cashed Out `{GpFormatter.Format((long)game.CurrentPayout)}`!";
                else
                    resultText = "You Lost!";

                embed.AddField("Result", resultText, false);
            }
            
            return embed;
        }

        public static DiscordComponent[] BuildButtons(HigherLowerGame game)
        {
            if (game.Status != HigherLowerGameStatus.Active)
            {
                // Add rematch buttons if needed, similar to Blackjack
                return Array.Empty<DiscordComponent>();
            }

            var hlService = ServerEnvironment.GetServerEnvironment().ServerManager.HigherLowerService;
            var higherMult = hlService.CalculateMultiplier(game.LastCard, true, game.CurrentRound);
            var lowerMult = hlService.CalculateMultiplier(game.LastCard, false, game.CurrentRound);

            var buttons = new List<DiscordComponent>
            {
                new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"hl_higher_{game.Id}", $"{higherMult.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}x", higherMult == 0, new DiscordComponentEmoji(1449749026353451049)),
                new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"hl_lower_{game.Id}", $"{lowerMult.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}x", lowerMult == 0, new DiscordComponentEmoji(1449752437471842374)),
                new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"hl_cashout_{game.Id}", "Cashout", game.CurrentRound < 1, new DiscordComponentEmoji("💰"))
            };

            return buttons.ToArray();
        }
    }
}
