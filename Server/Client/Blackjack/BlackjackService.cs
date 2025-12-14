using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Server.Client.Users;
using Server.Infrastructure.Database;

namespace Server.Client.Blackjack
{
    public class BlackjackService
    {
        private readonly DatabaseManager _databaseManager;

        public BlackjackService(DatabaseManager databaseManager)
        {
            _databaseManager = databaseManager;
        }

        public async Task<BlackjackGame> CreateGameAsync(User user, long betAmount)
        {
            if (user == null || betAmount <= 0 || user.Balance < betAmount)
                return null;

            try
            {
                var game = new BlackjackGame
                {
                    UserId = user.Id,
                    Identifier = user.Identifier,
                    BetAmount = betAmount,
                    Status = BlackjackGameStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // Create a fresh deck
                game.DeckState = CreateShuffledDeck();

                // Deal initial cards: Player, Dealer, Player, Dealer
                var playerHand = new BlackjackHand { BetAmount = betAmount };
                playerHand.Cards.Add(DrawCard(game.DeckState));
                game.DealerHand.Cards.Add(DrawCard(game.DeckState));
                playerHand.Cards.Add(DrawCard(game.DeckState));
                game.DealerHand.Cards.Add(DrawCard(game.DeckState));

                game.PlayerHands.Add(playerHand);

                // Check for immediate Blackjack
                var dealerUpCard = game.DealerHand.Cards[0];
                bool dealerHasTen = dealerUpCard.Rank == "10" || dealerUpCard.Rank == "J" || dealerUpCard.Rank == "Q" || dealerUpCard.Rank == "K";
                bool dealerHasAce = dealerUpCard.Rank == "A";

                // 1. If Dealer shows 10/Face, peek for Blackjack immediately.
                // if (dealerHasTen)
                // {
                //     if (game.DealerHand.IsBlackjack())
                //     {
                //         await FinishGameAsync(game);
                //     }
                // }

                // 2. If Player has Blackjack, finish immediately.
                // We do not offer insurance or even money if the player has Blackjack.
                if (game.Status == BlackjackGameStatus.Active && playerHand.IsBlackjack())
                {
                    playerHand.IsStanding = true;
                    game.CurrentHandIndex++;
                    await FinishGameAsync(game);
                }

                // Save to DB
                using (var cmd = new DatabaseCommand())
                {
                    cmd.SetCommand(@"
                        INSERT INTO blackjack_games 
                        (user_id, identifier, bet_amount, status, deck_state, dealer_hand, player_hands, current_hand_index, insurance_taken, created_at, updated_at)
                        VALUES (@user_id, @identifier, @bet_amount, @status, @deck_state, @dealer_hand, @player_hands, @current_hand_index, @insurance_taken, @created_at, @updated_at);
                        SELECT LAST_INSERT_ID();");
                    cmd.AddParameter("user_id", game.UserId);
                    cmd.AddParameter("identifier", game.Identifier);
                    cmd.AddParameter("bet_amount", game.BetAmount);
                    cmd.AddParameter("status", (int)game.Status);
                    cmd.AddParameter("deck_state", JsonSerializer.Serialize(game.DeckState));
                    cmd.AddParameter("dealer_hand", JsonSerializer.Serialize(game.DealerHand));
                    cmd.AddParameter("player_hands", JsonSerializer.Serialize(game.PlayerHands));
                    cmd.AddParameter("current_hand_index", game.CurrentHandIndex);
                    cmd.AddParameter("insurance_taken", game.InsuranceTaken);
                    cmd.AddParameter("created_at", game.CreatedAt);
                    cmd.AddParameter("updated_at", game.UpdatedAt);

                    var result = await cmd.ExecuteScalarAsync();
                    game.Id = Convert.ToInt32(result);
                }

                return game;
            }
            catch (Exception ex)
            {
                var env = ServerEnvironment.GetServerEnvironment();
                env.ServerManager.LoggerManager.LogError($"CreateGameAsync failed: {ex}");
            }

            return null;
        }

        public async Task<BlackjackGame> GetGameAsync(int gameId)
        {
            try
            {
                using (var cmd = new DatabaseCommand())
                {
                    cmd.SetCommand("SELECT * FROM blackjack_games WHERE id = @id LIMIT 1");
                    cmd.AddParameter("id", gameId);

                    using (var reader = await cmd.ExecuteDataReaderAsync())
                    {
                        if (reader != null && reader.Read())
                        {
                            return MapGame(reader);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var env = ServerEnvironment.GetServerEnvironment();
                env.ServerManager.LoggerManager.LogError($"GetGameAsync failed: {ex}");
            }

            return null;
        }

        public async Task<List<BlackjackGame>> GetActiveGamesByUserIdAsync(int userId)
        {
            var games = new List<BlackjackGame>();
            try
            {
                using (var cmd = new DatabaseCommand())
                {
                    cmd.SetCommand("SELECT * FROM blackjack_games WHERE user_id = @user_id AND status = @status");
                    cmd.AddParameter("user_id", userId);
                    cmd.AddParameter("status", (int)BlackjackGameStatus.Active);

                    using (var reader = await cmd.ExecuteDataReaderAsync())
                    {
                        while (reader != null && reader.Read())
                        {
                            games.Add(MapGame(reader));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var env = ServerEnvironment.GetServerEnvironment();
                env.ServerManager.LoggerManager.LogError($"GetActiveGamesByUserIdAsync failed: {ex}");
            }
            return games;
        }

        public async Task UpdateGameStatusAsync(int gameId, BlackjackGameStatus status)
        {
            try
            {
                using (var cmd = new DatabaseCommand())
                {
                    cmd.SetCommand("UPDATE blackjack_games SET status = @status, updated_at = @updated_at WHERE id = @id");
                    cmd.AddParameter("status", (int)status);
                    cmd.AddParameter("updated_at", DateTime.UtcNow);
                    cmd.AddParameter("id", gameId);
                    await cmd.ExecuteQueryAsync();
                }
            }
            catch (Exception ex)
            {
                var env = ServerEnvironment.GetServerEnvironment();
                env.ServerManager.LoggerManager.LogError($"UpdateGameStatusAsync failed: {ex}");
            }
        }

        public async Task<bool> HitAsync(BlackjackGame game)
        {
            var hand = game.GetCurrentHand();
            if (hand == null || hand.IsStanding || hand.IsBusted)
                return false;

            // Draw a card
            hand.Cards.Add(DrawCard(game.DeckState));

            var total = hand.GetTotal();

            // Check if busted
            if (total > 21)
            {
                hand.IsBusted = true;
                hand.IsStanding = true;
            }
            // Check for 21 (Auto-stand)
            else if (total == 21)
            {
                hand.IsStanding = true;
            }

            // If this was a doubled hand, automatically stand
            if (hand.IsDoubled)
            {
                hand.IsStanding = true;
            }

            // Check if all hands finished
            if (game.AllHandsFinished())
            {
                await FinishGameAsync(game);
            }
            else
            {
                // Move to next hand if current is finished
                if (hand.IsStanding || hand.IsBusted)
                {
                    game.CurrentHandIndex++;
                }
            }

            game.UpdatedAt = DateTime.UtcNow;
            await SaveGameAsync(game);

            return true;
        }

        public async Task<bool> StandAsync(BlackjackGame game)
        {
            var hand = game.GetCurrentHand();
            if (hand == null || hand.IsStanding)
                return false;

            hand.IsStanding = true;

            // Move to next hand
            game.CurrentHandIndex++;

            // Check if all hands finished
            if (game.AllHandsFinished())
            {
                await FinishGameAsync(game);
            }

            game.UpdatedAt = DateTime.UtcNow;
            await SaveGameAsync(game);

            return true;
        }

        public async Task<bool> DoubleAsync(BlackjackGame game, User user)
        {
            var hand = game.GetCurrentHand();
            if (hand == null || !hand.CanDouble() || user.Balance < hand.BetAmount)
                return false;

            // Double the bet
            hand.BetAmount *= 2;
            hand.IsDoubled = true;

            // Draw one card
            hand.Cards.Add(DrawCard(game.DeckState));

            // Check if busted
            if (hand.GetTotal() > 21)
            {
                hand.IsBusted = true;
            }

            // Automatically stand after doubling
            hand.IsStanding = true;

            // Move to next hand
            game.CurrentHandIndex++;

            // Check if all hands finished
            if (game.AllHandsFinished())
            {
                await FinishGameAsync(game);
            }

            game.UpdatedAt = DateTime.UtcNow;
            await SaveGameAsync(game);

            return true;
        }

        public async Task<bool> SplitAsync(BlackjackGame game, User user)
        {
            var hand = game.GetCurrentHand();
            if (hand == null || !hand.CanSplit() || user.Balance < game.BetAmount)
                return false;

            // Create a new hand with the second card
            var newHand = new BlackjackHand
            {
                BetAmount = hand.BetAmount
            };
            newHand.Cards.Add(hand.Cards[1]);
            hand.Cards.RemoveAt(1);

            // Draw a card for each hand
            hand.Cards.Add(DrawCard(game.DeckState));
            newHand.Cards.Add(DrawCard(game.DeckState));

            // Insert the new hand after the current hand
            game.PlayerHands.Insert(game.CurrentHandIndex + 1, newHand);

            // Check for Split Aces (Standard Rule: Only 1 card dealt, then auto-stand)
            bool isSplitAces = hand.Cards[0].Rank == "A";
            if (isSplitAces)
            {
                hand.IsStanding = true;
                newHand.IsStanding = true;
                // Since both stand, we might need to advance index or finish
                // But the loop/logic below handles index incrementing
            }

            // Check if current hand is 21 (e.g. split Aces or 10s)
            // Or if we just split Aces (auto-stand)
            if (hand.GetTotal() == 21 || isSplitAces)
            {
                hand.IsStanding = true; // Redundant but safe
                game.CurrentHandIndex++;
            }

            // Check if all hands finished (e.g. split Aces -> both 21 or just stood)
            if (game.AllHandsFinished())
            {
                await FinishGameAsync(game);
            }

            game.UpdatedAt = DateTime.UtcNow;
            await SaveGameAsync(game);

            return true;
        }

        public async Task<bool> TakeInsuranceAsync(BlackjackGame game, User user)
        {
            // Insurance can only be taken if dealer's first card is an Ace
            if (game.DealerHand.Cards.Count < 1 || game.DealerHand.Cards[0].Rank != "A")
                return false;

            if (game.InsuranceTaken)
                return false;

            long insuranceCost = game.BetAmount / 2;
            if (user.Balance < insuranceCost)
                return false;

            // Attempt to set insurance_taken in DB atomically to prevent race conditions
            try 
            {
                using (var cmd = new DatabaseCommand())
                {
                    cmd.SetCommand("UPDATE blackjack_games SET insurance_taken = 1 WHERE id = @id AND insurance_taken = 0");
                    cmd.AddParameter("id", game.Id);
                    int rows = await cmd.ExecuteQueryAsync();
                    if (rows == 0)
                        return false; // Already taken
                }
            }
            catch (Exception ex)
            {
                var env = ServerEnvironment.GetServerEnvironment();
                env.ServerManager.LoggerManager.LogError($"TakeInsuranceAsync atomic update failed: {ex}");
                return false;
            }

            game.InsuranceTaken = true;
            
            // Check if dealer has Blackjack
            if (game.DealerHand.IsBlackjack())
            {
                await FinishGameAsync(game);
            }

            game.UpdatedAt = DateTime.UtcNow;
            await SaveGameAsync(game);

            return true;
        }

        public async Task<bool> DeclineInsuranceAsync(BlackjackGame game)
        {
            if (game.InsuranceTaken || game.InsuranceDeclined)
                return false;

            game.InsuranceDeclined = true;

            // Check if dealer has Blackjack
            if (game.DealerHand.IsBlackjack())
            {
                await FinishGameAsync(game);
            }

            game.UpdatedAt = DateTime.UtcNow;
            await SaveGameAsync(game);

            return true;
        }

        private async Task FinishGameAsync(BlackjackGame game)
        {
            // Check if there are any non-busted player hands
            bool anyLiveHands = game.PlayerHands.Any(h => !h.IsBusted);

            // Dealer plays only if there are live hands AND not all hands are Blackjacks
            // If all player hands are Blackjacks, dealer doesn't draw (unless they have BJ, which is handled earlier)
            bool allBlackjacks = game.PlayerHands.All(h => h.IsBlackjack());
            
            if (anyLiveHands && !allBlackjacks)
            {
                while (game.DealerHand.GetTotal() < 17)
                {
                    game.DealerHand.Cards.Add(DrawCard(game.DeckState));
                }
            }

            if (game.DealerHand.GetTotal() > 21)
            {
                game.DealerHand.IsBusted = true;
            }

            // Calculate winnings
            var env = ServerEnvironment.GetServerEnvironment();
            var usersService = env.ServerManager.UsersService;
            var user = await usersService.GetUserAsync(game.Identifier);

            if (user != null)
            {
                long totalPayout = 0;
                long totalBet = 0;

                foreach (var hand in game.PlayerHands)
                {
                    totalBet += hand.BetAmount;

                    if (hand.IsBusted)
                    {
                        // Lost
                        continue;
                    }

                    var playerTotal = hand.GetTotal();
                    var dealerTotal = game.DealerHand.GetTotal();
                    // Standard Rule: Split hands cannot be Blackjack (only 21)
                    var isPlayerBlackjack = hand.IsBlackjack() && game.PlayerHands.Count == 1;
                    var isDealerBlackjack = game.DealerHand.IsBlackjack();

                    if (isPlayerBlackjack && !isDealerBlackjack)
                    {
                        // Blackjack! 3:2 payout
                        // Use floating point math to avoid integer truncation for odd amounts
                        long bonus = (long)(hand.BetAmount * 1.5);
                        totalPayout += hand.BetAmount + bonus;
                    }
                    else if (isPlayerBlackjack && isDealerBlackjack)
                    {
                        // Push
                        totalPayout += hand.BetAmount;
                    }
                    else if (game.DealerHand.IsBusted)
                    {
                        // Dealer busted, player wins
                        totalPayout += hand.BetAmount * 2;
                    }
                    else if (playerTotal > dealerTotal)
                    {
                        // Player wins
                        totalPayout += hand.BetAmount * 2;
                    }
                    else if (playerTotal == dealerTotal && !isDealerBlackjack)
                    {
                        // Push (only if dealer does NOT have Blackjack)
                        totalPayout += hand.BetAmount;
                    }
                    // else: player loses (including if Player has 21 vs Dealer Blackjack)
                }

                // Handle insurance
                if (game.InsuranceTaken)
                {
                    long insuranceCost = game.BetAmount / 2;
                    totalBet += insuranceCost; // Add insurance cost to total bet for tracking
                    
                    if (game.DealerHand.IsBlackjack())
                    {
                        // Insurance pays 2:1. 
                        // Cost X. Win 2X. Total return 3X.
                        totalPayout += insuranceCost * 3;
                    }
                }

                // Update user balance
                if (totalPayout > 0)
                {
                    await usersService.AddBalanceAsync(user.Identifier, totalPayout);
                }

                // Publish to Live Feed and Race
                long netChange = totalPayout - totalBet;
                bool isWin = netChange > 0;
                bool isPush = netChange == 0 && totalPayout > 0; // Push if money returned equals bet
                // Note: If totalPayout is 0, it's a loss, netChange = -totalBet.

                long displayAmount = isWin ? totalPayout : totalBet;
                // If push, displayAmount is totalBet (which equals totalPayout).

                env.ServerManager.LiveFeedService?.PublishBlackjack(displayAmount, isWin, isPush);
                
                if (!isPush)
                {
                    var raceName = user.DisplayName ?? user.Username ?? user.Identifier;
                    await env.ServerManager.RaceService.RegisterWagerAsync(user.Identifier, raceName, totalBet);
                }
            }

            game.Status = BlackjackGameStatus.Finished;
        }

        private async Task SaveGameAsync(BlackjackGame game)
        {
            try
            {
                using (var cmd = new DatabaseCommand())
                {
                    cmd.SetCommand(@"
                        UPDATE blackjack_games 
                        SET status = @status,
                            deck_state = @deck_state,
                            dealer_hand = @dealer_hand,
                            player_hands = @player_hands,
                            current_hand_index = @current_hand_index,
                            insurance_taken = @insurance_taken,
                            message_id = @message_id,
                            channel_id = @channel_id,
                            updated_at = @updated_at
                        WHERE id = @id");
                    cmd.AddParameter("id", game.Id);
                    cmd.AddParameter("status", (int)game.Status);
                    cmd.AddParameter("deck_state", JsonSerializer.Serialize(game.DeckState));
                    cmd.AddParameter("dealer_hand", JsonSerializer.Serialize(game.DealerHand));
                    cmd.AddParameter("player_hands", JsonSerializer.Serialize(game.PlayerHands));
                    cmd.AddParameter("current_hand_index", game.CurrentHandIndex);
                    cmd.AddParameter("insurance_taken", game.InsuranceTaken);
                    cmd.AddParameter("message_id", game.MessageId.HasValue ? (object)game.MessageId.Value : DBNull.Value);
                    cmd.AddParameter("channel_id", game.ChannelId.HasValue ? (object)game.ChannelId.Value : DBNull.Value);
                    cmd.AddParameter("updated_at", game.UpdatedAt);

                    await cmd.ExecuteQueryAsync();
                }
            }
            catch (Exception ex)
            {
                var env = ServerEnvironment.GetServerEnvironment();
                env.ServerManager.LoggerManager.LogError($"SaveGameAsync failed: {ex}");
            }
        }

        public async Task UpdateMessageInfoAsync(int gameId, ulong messageId, ulong channelId)
        {
            try
            {
                using (var cmd = new DatabaseCommand())
                {
                    cmd.SetCommand(@"
                        UPDATE blackjack_games 
                        SET message_id = @message_id, channel_id = @channel_id
                        WHERE id = @id");
                    cmd.AddParameter("id", gameId);
                    cmd.AddParameter("message_id", messageId);
                    cmd.AddParameter("channel_id", channelId);

                    await cmd.ExecuteQueryAsync();
                }
            }
            catch (Exception ex)
            {
                var env = ServerEnvironment.GetServerEnvironment();
                env.ServerManager.LoggerManager.LogError($"UpdateMessageInfoAsync failed: {ex}");
            }
        }

        private List<Card> CreateShuffledDeck()
        {
            var suits = new[] { "Hearts", "Diamonds", "Clubs", "Spades" };
            var ranks = new[] { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };
            
            // Use 6 decks (standard casino shoe)
            int numberOfDecks = 6;

            var deck = new List<Card>();
            for (int d = 0; d < numberOfDecks; d++)
            {
                foreach (var suit in suits)
                {
                    foreach (var rank in ranks)
                    {
                        deck.Add(new Card(suit, rank));
                    }
                }
            }

            // Shuffle using Fisher-Yates with Cryptographically Secure RNG
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                var temp = deck[i];
                deck[i] = deck[j];
                deck[j] = temp;
            }

            return deck;
        }

        private Card DrawCard(List<Card> deck)
        {
            if (deck.Count == 0)
            {
                // Reshuffle if deck is empty (shouldn't happen in normal game)
                deck.AddRange(CreateShuffledDeck());
            }

            var card = deck[0];
            deck.RemoveAt(0);
            return card;
        }

        private BlackjackGame MapGame(System.Data.Common.DbDataReader reader)
        {
            var game = new BlackjackGame
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                UserId = reader.GetInt32(reader.GetOrdinal("user_id")),
                Identifier = reader.GetString(reader.GetOrdinal("identifier")),
                BetAmount = reader.GetInt64(reader.GetOrdinal("bet_amount")),
                Status = (BlackjackGameStatus)reader.GetInt32(reader.GetOrdinal("status")),
                DeckState = JsonSerializer.Deserialize<List<Card>>(reader.GetString(reader.GetOrdinal("deck_state"))),
                DealerHand = JsonSerializer.Deserialize<BlackjackHand>(reader.GetString(reader.GetOrdinal("dealer_hand"))),
                PlayerHands = JsonSerializer.Deserialize<List<BlackjackHand>>(reader.GetString(reader.GetOrdinal("player_hands"))),
                CurrentHandIndex = reader.GetInt32(reader.GetOrdinal("current_hand_index")),
                InsuranceTaken = reader.GetBoolean(reader.GetOrdinal("insurance_taken")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updated_at"))
            };

            var messageIdOrdinal = reader.GetOrdinal("message_id");
            var channelIdOrdinal = reader.GetOrdinal("channel_id");

            if (!reader.IsDBNull(messageIdOrdinal))
                game.MessageId = (ulong)reader.GetInt64(messageIdOrdinal);

            if (!reader.IsDBNull(channelIdOrdinal))
                game.ChannelId = (ulong)reader.GetInt64(channelIdOrdinal);

            return game;
        }
    }
}
