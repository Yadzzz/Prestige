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

            if (string.IsNullOrWhiteSpace(amount))
            {
                await ctx.RespondAsync("Please specify an amount. Usage: `!bj <amount>` (e.g. `!bj 100m`).");
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

            var game = await blackjackService.CreateGameAsync(user, betAmount);
            if (game == null)
            {
                await usersService.AddBalanceAsync(user.Identifier, betAmount);
                await ctx.RespondAsync("Failed to create blackjack game. Please try again later.");
                return;
            }

            // If game finished immediately (e.g. Blackjack), refresh user balance
            if (game.Status == BlackjackGameStatus.Finished)
            {
                user = await usersService.GetUserAsync(user.Identifier);
            }

            var embed = BuildGameEmbed(game, user, ctx.Client);
            var buttons = BuildButtons(game);

            var builder = new DiscordMessageBuilder().AddEmbed(embed);
            if (buttons.Length > 0)
            {
                builder.AddActionRowComponent(new DiscordActionRowComponent(buttons));
            }

            DSharpPlus.Entities.DiscordMessage message;
            try
            {
                message = await ctx.RespondAsync(builder);
            }
            catch (DSharpPlus.Exceptions.BadRequestException)
            {
                // Retry without emojis in buttons
                builder.ClearComponents();
                var textButtons = BuildButtons(game, useEmojis: false);
                if (textButtons.Length > 0)
                {
                    builder.AddActionRowComponent(new DiscordActionRowComponent(textButtons));
                }
                message = await ctx.RespondAsync(builder);
            }

            await blackjackService.UpdateMessageInfoAsync(game.Id, message.Id, message.Channel.Id);
        }

        public static DiscordEmbedBuilder BuildGameEmbed(BlackjackGame game, User user, DiscordClient client = null)
        {
            var embed = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Gold)
                .WithFooter($"{ServerConfiguration.ServerName} • {DateTime.UtcNow:yyyy-MM-dd HH:mm}")
                .WithTimestamp(null); // Timestamp handled in footer text

            var currentHand = game.GetCurrentHand();
            var dealerTotal = game.DealerHand.GetTotal();
            var isGameFinished = game.Status == BlackjackGameStatus.Finished;

            // Title
            var diamond = DiscordIds.BlackjackDiamondsEmojiId > 0 ? $"<:diamond:{DiscordIds.BlackjackDiamondsEmojiId}>" : "♦️";
            var club = DiscordIds.BlackjackClubsEmojiId > 0 ? $"<:club:{DiscordIds.BlackjackClubsEmojiId}>" : "♣️";
            var spade = DiscordIds.BlackjackSpadesEmojiId > 0 ? $"<:spade:{DiscordIds.BlackjackSpadesEmojiId}>" : "♠️";
            var heart = DiscordIds.BlackjackHeartsEmojiId > 0 ? $"<:heart:{DiscordIds.BlackjackHeartsEmojiId}>" : "♥️";
            
            embed.WithTitle($"{diamond} {club} Blackjack #{game.Id} {spade} {heart}");

            // Image Logic
            string imageUrl = "https://i.imgur.com/baRNgRg.gif"; // Default / Start Game
            string resultText = "";
            bool showBet = !isGameFinished;

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
                        totalPayout += insuranceCost * 3;
                    }
                }

                long netChange = totalPayout - totalBet;

                if (netChange > 0)
                {
                    if (game.PlayerHands.Any(h => h.IsBlackjack()))
                    {
                        embed.WithColor(DiscordColor.Magenta); // Purple for Blackjack
                        imageUrl = "https://i.imgur.com/ecYpqiV.gif"; // Blackjack
                    }
                    else
                    {
                        embed.WithColor(DiscordColor.Green);
                        imageUrl = "https://i.imgur.com/J2JWsJD.gif"; // Win
                    }
                    resultText = $"🎉 You won `{GpFormatter.Format(totalPayout)}`\n";
                }
                else if (netChange < 0)
                {
                    embed.WithColor(DiscordColor.Red);
                    resultText = $"You lost `{GpFormatter.Format(Math.Abs(netChange))}`\n";
                    imageUrl = "https://i.imgur.com/jtiAW54.gif"; // Lost
                }
                else
                {
                    embed.WithColor(DiscordColor.Orange);
                    resultText = $"The dealer has scored the same as you.\nYou received back `{GpFormatter.Format(totalPayout)}`\n";
                    imageUrl = "https://i.imgur.com/VOnBLHK.gif"; // Draw
                }
            }
            else
            {
                // Game in progress
                if (currentHand != null && currentHand.IsDoubled)
                {
                    imageUrl = "https://i.imgur.com/sANWb6u.gif"; // Double
                }
            }

            embed.WithThumbnail(imageUrl);

            // Bet Info
            var betAmount = currentHand?.BetAmount ?? game.BetAmount;
            if (game.PlayerHands.Count > 1)
            {
                betAmount = game.PlayerHands.Sum(h => h.BetAmount);
            }
            var betLine = showBet ? $"Bet: `{GpFormatter.Format(betAmount)}`\n" : "";
            var description = $"{resultText}{betLine}Balance: `{GpFormatter.Format(user.Balance)}`\n\n";
            
            // Player hands
            string playerHandsText = "";
            for (int i = 0; i < game.PlayerHands.Count; i++)
            {
                var hand = game.PlayerHands[i];
                var handTotal = hand.GetTotal();
                var handLabel = game.PlayerHands.Count > 1 ? $"Your Hand #{i + 1}" : "Your hand";
                var isCurrent = i == game.CurrentHandIndex && !isGameFinished;

                var handDisplay = hand.GetHandDisplay(client);
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
                dealerHandText = $"**Dealer's hand ({dealerTotal})**\n{game.DealerHand.GetHandDisplay(client)}{(game.DealerHand.IsBusted ? " 💥 **BUST**" : "")}";
            }
            else
            {
                // Calculate visible total for dealer (first card only)
                var visibleCard = game.DealerHand.Cards.FirstOrDefault();
                var visibleTotal = visibleCard != null ? visibleCard.GetValue(0) : 0;
                dealerHandText = $"**Dealer's hand ({visibleTotal})**\n{game.DealerHand.GetHandDisplay(client, hideHoleCard: true)}";
            }

            embed.WithDescription($"{description}{playerHandsText}{dealerHandText}");

            // Insurance Status
            bool isInsuranceOffered = !isGameFinished && 
                                      game.DealerHand.Cards.Count > 0 && 
                                      game.DealerHand.Cards[0].Rank == "A" && 
                                      !game.InsuranceTaken && 
                                      !game.InsuranceDeclined;

            if (isInsuranceOffered)
            {
                embed.AddField("🛡️ Insurance Offered", "Dealer shows an Ace. Pays 2:1 if Dealer has Blackjack.\nCost: `" + GpFormatter.Format(game.BetAmount / 2) + "`");
            }
            else if (game.InsuranceTaken)
            {
                embed.AddField("🛡️ Insurance Active", "You are insured against Dealer Blackjack.");
            }

            return embed;
        }

        public static DiscordComponent[] BuildButtons(BlackjackGame game, bool useEmojis = true)
        {
            if (game.Status == BlackjackGameStatus.Finished)
            {
                var rematchButtons = new System.Collections.Generic.List<DiscordButtonComponent>();

                // Rematch buttons
                if (useEmojis)
                {
                    rematchButtons.Add(new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"bj_half_{game.Id}", "1/2", emoji: new DiscordComponentEmoji(DiscordIds.CoinflipHalfEmojiId)));
                    rematchButtons.Add(new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"bj_rm_{game.Id}", "RM", emoji: new DiscordComponentEmoji(DiscordIds.CoinflipRmEmojiId)));
                    rematchButtons.Add(new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"bj_x2_{game.Id}", "X2", emoji: new DiscordComponentEmoji(DiscordIds.CoinflipX2EmojiId)));
                    rematchButtons.Add(new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"bj_max_{game.Id}", "Max", emoji: new DiscordComponentEmoji(DiscordIds.CoinflipMaxEmojiId)));
                }
                else
                {
                    rematchButtons.Add(new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"bj_half_{game.Id}", "1/2"));
                    rematchButtons.Add(new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"bj_rm_{game.Id}", "RM"));
                    rematchButtons.Add(new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"bj_x2_{game.Id}", "X2"));
                    rematchButtons.Add(new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"bj_max_{game.Id}", "Max"));
                }

                return rematchButtons.ToArray();
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
                if (useEmojis && DiscordIds.BlackjackHitEmojiId > 0)
                    buttons.Add(new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"bj_hit_{game.Id}", "Hit", emoji: new DiscordComponentEmoji(DiscordIds.BlackjackHitEmojiId)));
                else
                    buttons.Add(new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"bj_hit_{game.Id}", "Hit"));
            }

            // Stand button
            // Always show unless busted or standing
            if (!currentHand.IsStanding && !currentHand.IsBusted)
            {
                if (useEmojis && DiscordIds.BlackjackStandEmojiId > 0)
                    buttons.Add(new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"bj_stand_{game.Id}", "Stand", emoji: new DiscordComponentEmoji(DiscordIds.BlackjackStandEmojiId)));
                else
                    buttons.Add(new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"bj_stand_{game.Id}", "Stand"));
            }

            // Double button
            // Hide if 21
            if (currentHand.CanDouble() && !is21)
            {
                if (useEmojis && DiscordIds.BlackjackDoubleEmojiId > 0)
                    buttons.Add(new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"bj_double_{game.Id}", "Double", emoji: new DiscordComponentEmoji(DiscordIds.BlackjackDoubleEmojiId)));
                else
                    buttons.Add(new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"bj_double_{game.Id}", "Double"));
            }

            // Split button
            // Hide if 21 (unless 2 Aces? but 2 Aces is 12)
            if (currentHand.CanSplit())
            {
                if (useEmojis && DiscordIds.BlackjackSplitEmojiId > 0)
                    buttons.Add(new DiscordButtonComponent(DiscordButtonStyle.Secondary, $"bj_split_{game.Id}", "Split", emoji: new DiscordComponentEmoji(DiscordIds.BlackjackSplitEmojiId)));
                else
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
