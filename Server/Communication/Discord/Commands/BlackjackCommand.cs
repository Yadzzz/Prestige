using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Server.Client.Blackjack;
using Server.Client.Users;
using Server.Client.Utils;
using Server.Infrastructure;
using Server.Infrastructure.Discord;

namespace Server.Communication.Discord.Commands
{
    public class BlackjackCommand : BaseCommandModule
    {
        private static readonly TimeSpan RateLimitInterval = TimeSpan.FromSeconds(1);

        [Command("blackjack")]
        [Aliases("bj")]
        public async Task Blackjack(CommandContext ctx, string amount = null)
        {
            if (!await DiscordChannelPermissionService.EnforceBlackjackChannelAsync(ctx))
            {
                return;
            }

            if (RateLimiter.IsRateLimited(ctx.User.Id, "blackjack", RateLimitInterval))
            {
                await ctx.RespondAsync("You're doing that too fast. Please wait a moment.");
                return;
            }

            var env = ServerEnvironment.GetServerEnvironment();
            var serverManager = env.ServerManager;
            var usersService = serverManager.UsersService;
            var blackjackService = serverManager.BlackjackService;

            var user = await usersService.EnsureUserAsync(ctx.User.Id.ToString(), ctx.User.Username, ctx.Member.DisplayName);
            if (user == null)
                return;

            long betAmount;

            if (string.IsNullOrWhiteSpace(amount))
            {
                betAmount = user.Balance;
            }
            else if (!GpParser.TryParseAmountInK(amount, out betAmount))
            {
                await ctx.RespondAsync("Invalid amount. Examples: `!blackjack 100`, `!bj 0.5`, `!bj 1b`, `!bj 1000m`, or `!blackjack` for all-in.");
                return;
            }

            if (betAmount < GpFormatter.MinimumBetAmountK)
            {
                await ctx.RespondAsync($"Minimum bet is {GpFormatter.Format(GpFormatter.MinimumBetAmountK)}.");
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

            var game = await blackjackService.CreateGameAsync(user, betAmount);
            if (game == null)
            {
                await usersService.AddBalanceAsync(user.Identifier, betAmount);
                await ctx.RespondAsync("Failed to create blackjack game. Please try again later.");
                return;
            }

            var embed = BuildGameEmbed(game, user);
            var buttons = BuildButtons(game);

            var builder = new DiscordMessageBuilder().AddEmbed(embed);
            if (buttons.Length > 0)
            {
                builder.AddComponents(buttons);
            }

            var message = await ctx.RespondAsync(builder);

            await blackjackService.UpdateMessageInfoAsync(game.Id, message.Id, message.Channel.Id);
        }

        public static DiscordEmbedBuilder BuildGameEmbed(BlackjackGame game, User user)
        {
            var embed = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.DarkBlue)
                .WithFooter($"{ServerConfiguration.ServerName} • {DateTime.UtcNow:yyyy-MM-dd HH:mm}")
                .WithTimestamp(null); // Timestamp handled in footer text

            var currentHand = game.GetCurrentHand();
            var dealerTotal = game.DealerHand.GetTotal();
            var isGameFinished = game.Status == BlackjackGameStatus.Finished;

            // Title
            embed.WithTitle($"♦️ ♣️ Blackjack #{game.Id} ♠️ ♥️");

            // Bet Info
            var betInfo = $"Bet: **{GpFormatter.Format(currentHand?.BetAmount ?? game.BetAmount)}**";
            if (game.PlayerHands.Count > 1)
            {
                var totalCurrentBet = game.PlayerHands.Sum(h => h.BetAmount);
                betInfo = $"Total Bet: **{GpFormatter.Format(totalCurrentBet)}**";
            }

            // Add Balance info
            betInfo += $" • Balance: **{GpFormatter.Format(user.Balance)}**";
            
            // Player hands
            string playerHandsText = "";
            for (int i = 0; i < game.PlayerHands.Count; i++)
            {
                var hand = game.PlayerHands[i];
                var handTotal = hand.GetTotal();
                var handLabel = game.PlayerHands.Count > 1 ? $"Your Hand #{i + 1}" : "Your hand";
                var isCurrent = i == game.CurrentHandIndex && !isGameFinished;

                var handDisplay = hand.GetHandDisplay();
                var statusText = "";
                
                if (hand.IsBlackjack())
                    statusText = " 🎉 **BLACKJACK**";
                else if (hand.IsBusted)
                    statusText = " 💥 **BUST**";
                else if (hand.IsStanding)
                    statusText = " ✋";

                // Format: "Your hand (13)\n[Cards] [Status]"
                playerHandsText += $"**{handLabel} ({handTotal})**\n{handDisplay}{statusText}\n\n";
            }

            // Dealer hand
            string dealerHandText = "";
            if (isGameFinished)
            {
                dealerHandText = $"**Dealer's hand ({dealerTotal})**\n{game.DealerHand.GetHandDisplay()}{(game.DealerHand.IsBusted ? " 💥 **BUST**" : "")}";
            }
            else
            {
                // Calculate visible total for dealer (first card only)
                var visibleCard = game.DealerHand.Cards.FirstOrDefault();
                var visibleTotal = visibleCard != null ? visibleCard.GetValue(0) : 0;
                dealerHandText = $"**Dealer's hand ({visibleTotal})**\n{game.DealerHand.GetHandDisplay(hideHoleCard: true)}";
            }

            embed.WithDescription($"{betInfo}\n\n{playerHandsText}{dealerHandText}");

            // Insurance Status
            bool isInsuranceOffered = !isGameFinished && 
                                      game.DealerHand.Cards.Count > 0 && 
                                      game.DealerHand.Cards[0].Rank == "A" && 
                                      !game.InsuranceTaken && 
                                      !game.InsuranceDeclined;

            if (isInsuranceOffered)
            {
                embed.AddField("🛡️ Insurance Offered", "Dealer shows an Ace. Pays 2:1 if Dealer has Blackjack.\nCost: **" + GpFormatter.Format(game.BetAmount / 2) + "**");
            }
            else if (game.InsuranceTaken)
            {
                embed.AddField("🛡️ Insurance Active", "You are insured against Dealer Blackjack.");
            }

            // Game result
            if (isGameFinished)
            {
                long totalPayout = 0;
                long totalBet = 0;

                foreach (var hand in game.PlayerHands)
                {
                    totalBet += hand.BetAmount;

                    if (hand.IsBusted)
                        continue;

                    var playerTotal = hand.GetTotal();
                    var isPlayerBlackjack = hand.IsBlackjack();
                    var isDealerBlackjack = game.DealerHand.IsBlackjack();

                    if (isPlayerBlackjack && !isDealerBlackjack)
                    {
                        totalPayout += hand.BetAmount + (hand.BetAmount * 3 / 2);
                    }
                    else if (isPlayerBlackjack && isDealerBlackjack)
                    {
                        totalPayout += hand.BetAmount;
                    }
                    else if (game.DealerHand.IsBusted)
                    {
                        totalPayout += hand.BetAmount * 2;
                    }
                    else if (playerTotal > dealerTotal)
                    {
                        totalPayout += hand.BetAmount * 2;
                    }
                    else if (playerTotal == dealerTotal)
                    {
                        totalPayout += hand.BetAmount;
                    }
                }

                if (game.InsuranceTaken)
                {
                    long insuranceCost = game.BetAmount / 2;
                    totalBet += insuranceCost;

                    if (game.DealerHand.IsBlackjack())
                    {
                        // Insurance pays 2:1. Return original insurance bet + 2x profit = 3x total.
                        totalPayout += insuranceCost * 3;
                    }
                }

                long netChange = totalPayout - totalBet;

                if (netChange > 0)
                {
                    embed.WithColor(DiscordColor.Green);
                    embed.AddField("Result", $"🎉 **You won {GpFormatter.Format(netChange)}!**\nPayout: **{GpFormatter.Format(totalPayout)}**\nBalance: **{GpFormatter.Format(user.Balance)}**");
                }
                else if (netChange < 0)
                {
                    embed.WithColor(DiscordColor.Red);
                    embed.AddField("Result", $"💀 **You lost {GpFormatter.Format(Math.Abs(netChange))}**\nBalance: **{GpFormatter.Format(user.Balance)}**");
                }
                else
                {
                    embed.WithColor(DiscordColor.Yellow);
                    embed.AddField("Result", $"🤝 **Push! Your bet is returned.**\nBalance: **{GpFormatter.Format(user.Balance)}**");
                }
            }

            return embed;
        }

        public static DiscordComponent[] BuildButtons(BlackjackGame game)
        {
            if (game.Status == BlackjackGameStatus.Finished)
            {
                return Array.Empty<DiscordComponent>();
            }

            var currentHand = game.GetCurrentHand();
            if (currentHand == null)
            {
                return Array.Empty<DiscordComponent>();
            }

            var buttons = new System.Collections.Generic.List<DiscordButtonComponent>();

            // Check if we are waiting for dealer (e.g. player has 21/BJ but dealer has Ace)
            bool isInsuranceOffered = game.DealerHand.Cards.Count > 0 
                                      && game.DealerHand.Cards[0].Rank == "A" 
                                      && !game.InsuranceTaken 
                                      && !game.InsuranceDeclined;
            bool is21 = currentHand.GetTotal() == 21;

            // Hit button
            // Hide if 21
            if (!currentHand.IsStanding && !currentHand.IsBusted && !is21)
            {
                buttons.Add(new DiscordButtonComponent(DiscordButtonStyle.Primary, $"bj_hit_{game.Id}", "Hit"));
            }

            // Stand button
            // Always show unless busted or standing
            if (!currentHand.IsStanding && !currentHand.IsBusted)
            {
                buttons.Add(new DiscordButtonComponent(DiscordButtonStyle.Success, $"bj_stand_{game.Id}", "Stand"));
            }

            // Double button
            // Hide if 21
            if (currentHand.CanDouble() && !is21)
            {
                buttons.Add(new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"bj_double_{game.Id}", "Double x2"));
            }

            // Split button
            // Hide if 21 (unless 2 Aces? but 2 Aces is 12)
            if (currentHand.CanSplit())
            {
                buttons.Add(new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"bj_split_{game.Id}", "Split"));
            }

            // Insurance button
            if (isInsuranceOffered && currentHand.Cards.Count == 2)
            {
                buttons.Add(new DiscordButtonComponent(DiscordButtonStyle.Danger, $"bj_ins_{game.Id}", "Insurance"));
            }

            return buttons.ToArray();
        }
    }
}
