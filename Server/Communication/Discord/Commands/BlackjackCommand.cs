using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Server.Client.Blackjack;
using Server.Client.Users;
using Server.Client.Utils;
using Server.Infrastructure;
using Server.Infrastructure.Discord;

namespace Server.Communication.Discord.Commands
{
    public class BlackjackCommand : ModuleBase<SocketCommandContext>
    {
        private static readonly TimeSpan RateLimitInterval = TimeSpan.FromSeconds(1);

        [Command("blackjack")]
        [Alias("bj")]
        public async Task Blackjack(string amount = null)
        {
            if (!await DiscordChannelPermissionService.EnforceBlackjackChannelAsync(Context))
            {
                return;
            }

            if (RateLimiter.IsRateLimited(Context.User.Id, "blackjack", RateLimitInterval))
            {
                await ReplyAsync("You're doing that too fast. Please wait a moment.");
                return;
            }

            if (string.IsNullOrWhiteSpace(amount))
            {
                await ReplyAsync("Please specify an amount. Usage: `!bj <amount>` (e.g. `!bj 100m`).");
                return;
            }

            var env = ServerEnvironment.GetServerEnvironment();
            var serverManager = env.ServerManager;
            var usersService = serverManager.UsersService;
            var blackjackService = serverManager.BlackjackService;

            var displayName = (Context.User as SocketGuildUser)?.DisplayName ?? Context.User.Username;
            var user = await usersService.EnsureUserAsync(Context.User.Id.ToString(), Context.User.Username, displayName);
            if (user == null)
                return;

            long betAmount;

            if (string.IsNullOrWhiteSpace(amount))
            {
                betAmount = user.Balance;
            }
            else if (!GpParser.TryParseAmountInK(amount, out betAmount))
            {
                await ReplyAsync("Invalid amount. Examples: `!blackjack 100`, `!bj 0.5`, `!bj 1b`, `!bj 1000m`, or `!blackjack` for all-in.");
                return;
            }

            if (betAmount < GpFormatter.MinimumBetAmountK)
            {
                await ReplyAsync($"Minimum bet is {GpFormatter.Format(GpFormatter.MinimumBetAmountK)}.");
                return;
            }

            if (user.Balance < betAmount)
            {
                await ReplyAsync("You don't have enough balance for this bet.");
                return;
            }

            if (!await usersService.RemoveBalanceAsync(user.Identifier, betAmount))
            {
                await ReplyAsync("Failed to lock balance for this game. Please try again.");
                return;
            }

            // Update local user balance for display
            user.Balance -= betAmount;

            var game = await blackjackService.CreateGameAsync(user, betAmount);
            if (game == null)
            {
                await usersService.AddBalanceAsync(user.Identifier, betAmount);
                await ReplyAsync("Failed to create blackjack game. Please try again later.");
                return;
            }

            // If game finished immediately (e.g. Blackjack), refresh user balance
            if (game.Status == BlackjackGameStatus.Finished)
            {
                user = await usersService.GetUserAsync(user.Identifier);
            }

            var embed = BuildGameEmbed(game, user, Context.Client);
            var buttons = BuildButtons(game);

            var builder = new ComponentBuilder();
            foreach (var btn in buttons)
            {
                builder.WithButton(btn);
            }

            var message = await ReplyAsync(embed: embed.Build(), components: builder.Build());
            await blackjackService.UpdateMessageInfoAsync(game.Id, message.Id, message.Channel.Id);
        }

        public static EmbedBuilder BuildGameEmbed(BlackjackGame game, User user, DiscordSocketClient client = null)
        {
            var embed = new EmbedBuilder()
                .WithColor(Color.Gold)
                .WithFooter($"{ServerConfiguration.ServerName} • {DateTime.UtcNow:yyyy-MM-dd HH:mm}")
                .WithCurrentTimestamp();

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
                        embed.WithColor(Color.Magenta); // Purple for Blackjack
                        imageUrl = "https://i.imgur.com/ecYpqiV.gif"; // Blackjack
                    }
                    else
                    {
                        embed.WithColor(Color.Green);
                        imageUrl = "https://i.imgur.com/J2JWsJD.gif"; // Win
                    }
                    resultText = $"Win: **{GpFormatter.Format(totalPayout)}**\n";
                }
                else if (netChange < 0)
                {
                    embed.WithColor(Color.Red);
                    resultText = $"Lost: **{GpFormatter.Format(Math.Abs(netChange))}**\n";
                    imageUrl = "https://i.imgur.com/jtiAW54.gif"; // Lost
                }
                else
                {
                    embed.WithColor(Color.Orange);
                    resultText = $"Push: **{GpFormatter.Format(totalPayout)}**\n";
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

            embed.WithThumbnailUrl(imageUrl);

            // Bet Info
            var betAmount = currentHand?.BetAmount ?? game.BetAmount;
            if (game.PlayerHands.Count > 1)
            {
                betAmount = game.PlayerHands.Sum(h => h.BetAmount);
            }
            
            string description;
            if (isGameFinished)
            {
                description = $"{resultText}Balance: **{GpFormatter.Format(user.Balance)}**\n\n";
            }
            else
            {
                description = $"{resultText}Bet: **{GpFormatter.Format(betAmount)}**\nBalance: **{GpFormatter.Format(user.Balance)}**\n\n";
            }
            
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
                embed.AddField("🛡️ Insurance Offered", "Dealer shows an Ace. Pays 2:1 if Dealer has Blackjack.\nCost: **" + GpFormatter.Format(game.BetAmount / 2) + "**");
            }
            else if (game.InsuranceTaken)
            {
                embed.AddField("🛡️ Insurance Active", "You are insured against Dealer Blackjack.");
            }

            return embed;
        }

        public static ButtonBuilder[] BuildButtons(BlackjackGame game, bool useEmojis = true)
        {
            if (game.Status == BlackjackGameStatus.Finished)
            {
                return Array.Empty<ButtonBuilder>();
            }

            var currentHand = game.GetCurrentHand();
            if (currentHand == null)
            {
                return Array.Empty<ButtonBuilder>();
            }

            var buttons = new System.Collections.Generic.List<ButtonBuilder>();

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
                    buttons.Add(new ButtonBuilder("Hit", $"bj_hit_{game.Id}", ButtonStyle.Secondary, emote: Emote.Parse($"<:e:{DiscordIds.BlackjackHitEmojiId}>")));
                else
                    buttons.Add(new ButtonBuilder("Hit", $"bj_hit_{game.Id}", ButtonStyle.Secondary));
            }

            // Stand button
            // Always show unless busted or standing
            if (!currentHand.IsStanding && !currentHand.IsBusted)
            {
                if (useEmojis && DiscordIds.BlackjackStandEmojiId > 0)
                    buttons.Add(new ButtonBuilder("Stand", $"bj_stand_{game.Id}", ButtonStyle.Secondary, emote: Emote.Parse($"<:e:{DiscordIds.BlackjackStandEmojiId}>")));
                else
                    buttons.Add(new ButtonBuilder("Stand", $"bj_stand_{game.Id}", ButtonStyle.Secondary));
            }

            // Double button
            // Hide if 21
            if (currentHand.CanDouble() && !is21)
            {
                if (useEmojis && DiscordIds.BlackjackDoubleEmojiId > 0)
                    buttons.Add(new ButtonBuilder("Double", $"bj_double_{game.Id}", ButtonStyle.Secondary, emote: Emote.Parse($"<:e:{DiscordIds.BlackjackDoubleEmojiId}>")));
                else
                    buttons.Add(new ButtonBuilder("Double", $"bj_double_{game.Id}", ButtonStyle.Secondary));
            }

            // Split button
            // Hide if 21 (unless 2 Aces? but 2 Aces is 12)
            if (currentHand.CanSplit())
            {
                if (useEmojis && DiscordIds.BlackjackSplitEmojiId > 0)
                    buttons.Add(new ButtonBuilder("Split", $"bj_split_{game.Id}", ButtonStyle.Secondary, emote: Emote.Parse($"<:e:{DiscordIds.BlackjackSplitEmojiId}>")));
                else
                    buttons.Add(new ButtonBuilder("Split", $"bj_split_{game.Id}", ButtonStyle.Secondary));
            }

            // Insurance button
            if (isInsuranceOffered && currentHand.Cards.Count == 2)
            {
                buttons.Add(new ButtonBuilder("Insurance", $"bj_ins_{game.Id}", ButtonStyle.Danger));
            }

            return buttons.ToArray();
        }
    }
}
