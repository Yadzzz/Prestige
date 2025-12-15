using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Server.Client.Blackjack;
using Server.Client.Users;
using Server.Infrastructure;
using Server.Infrastructure.Database;

namespace Server.Client.HigherLower
{
    public class HigherLowerService
    {
        private readonly DatabaseManager _databaseManager;
        private const decimal HouseEdge = 0.03m;
        private const decimal MinMultiplier = 1.05m;
        private const decimal MaxMultiplier = 10.00m;

        public HigherLowerService(DatabaseManager databaseManager)
        {
            _databaseManager = databaseManager;
        }

        public decimal CalculateMultiplier(Card card, bool isHigher, int currentRound)
        {
            int value = HigherLowerGame.GetCardValue(card);
            // Total cards: 13 (A=1 ... K=13)
            // Higher wins if New > Old. Count of cards > value is (13 - value).
            // Lower wins if New < Old. Count of cards < value is (value - 1).
            int winningOutcomes = isHigher ? (13 - value) : (value - 1);

            if (winningOutcomes <= 0) return 0m; // Impossible to win

            decimal probability = (decimal)winningOutcomes / 13.0m;
            decimal rawMultiplier = 1.0m / probability;
            decimal maxProfit = rawMultiplier - 1.0m;

            // Progressive Profit Scaling
            // Instead of a flat penalty, we scale the "Profit" portion of the multiplier.
            // Round 0: Player gets 25% of the fair profit. (Max possible ~4x)
            // Round 10: Player gets 97% of the fair profit. (Max possible ~12.6x, capped at 10x)
            // This equation naturally prevents high multipliers in early rounds without artificial round-gating.
            
            decimal startScale = 0.25m; // Start at 25% profit
            decimal endScale = 1.0m - HouseEdge; // End at 97% profit
            decimal roundsToMax = 10.0m;
            
            decimal growthPerRound = (endScale - startScale) / roundsToMax;
            decimal currentScale = startScale + (currentRound * growthPerRound);
            
            // Cap at max profitability (3% edge)
            if (currentScale > endScale) currentScale = endScale;

            decimal multiplier = 1.0m + (maxProfit * currentScale);

            // Apply caps
            if (multiplier < MinMultiplier) multiplier = MinMultiplier;
            if (multiplier > MaxMultiplier) multiplier = MaxMultiplier;

            return Math.Round(multiplier, 2);
        }

        public async Task<HigherLowerGame> CreateGameAsync(User user, long betAmount, int maxRounds = 10)
        {
            if (user == null || betAmount <= 0)
                return null;

            try
            {
                var game = new HigherLowerGame
                {
                    UserId = user.Id,
                    Identifier = user.Identifier,
                    BetAmount = betAmount,
                    CurrentPayout = betAmount,
                    CurrentRound = 0,
                    MaxRounds = maxRounds,
                    Status = HigherLowerGameStatus.Active,
                    LastCard = GenerateRandomCard(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                game.CardHistory.Add(game.LastCard);

                using (var cmd = new DatabaseCommand())
                {
                    cmd.SetCommand(@"
                        INSERT INTO higher_lower_games 
                        (user_id, identifier, bet_amount, current_payout, current_round, max_rounds, status, last_card, card_history, created_at, updated_at)
                        VALUES (@user_id, @identifier, @bet_amount, @current_payout, @current_round, @max_rounds, @status, @last_card, @card_history, @created_at, @updated_at);
                        SELECT LAST_INSERT_ID();");
                    cmd.AddParameter("user_id", game.UserId);
                    cmd.AddParameter("identifier", game.Identifier);
                    cmd.AddParameter("bet_amount", game.BetAmount);
                    cmd.AddParameter("current_payout", game.CurrentPayout);
                    cmd.AddParameter("current_round", game.CurrentRound);
                    cmd.AddParameter("max_rounds", game.MaxRounds);
                    cmd.AddParameter("status", (int)game.Status);
                    cmd.AddParameter("last_card", JsonSerializer.Serialize(game.LastCard));
                    cmd.AddParameter("card_history", JsonSerializer.Serialize(game.CardHistory));
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

        public async Task<HigherLowerGame> GetGameAsync(int gameId)
        {
            try
            {
                using (var cmd = new DatabaseCommand())
                {
                    cmd.SetCommand("SELECT * FROM higher_lower_games WHERE id = @id LIMIT 1");
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

        public async Task<(HigherLowerGame Game, bool IsWin, Card NewCard)> GuessAsync(int gameId, bool guessHigher)
        {
            var game = await GetGameAsync(gameId);
            if (game == null || game.Status != HigherLowerGameStatus.Active)
                return (game, false, null);

            var newCard = GenerateRandomCard();
            var oldValue = HigherLowerGame.GetCardValue(game.LastCard);
            var newValue = HigherLowerGame.GetCardValue(newCard);

            bool isCorrect = false;
            if (guessHigher)
            {
                isCorrect = newValue > oldValue;
            }
            else
            {
                isCorrect = newValue < oldValue;
            }

            // Tie is loss
            if (newValue == oldValue)
                isCorrect = false;

            var previousCard = game.LastCard;
            game.LastCard = newCard;
            game.CardHistory.Add(newCard);
            game.UpdatedAt = DateTime.UtcNow;

            if (isCorrect)
            {
                decimal multiplier = CalculateMultiplier(previousCard, guessHigher, game.CurrentRound);
                game.CurrentPayout *= multiplier;
                game.CurrentRound++;

                if (game.CurrentRound >= game.MaxRounds)
                {
                    game.Status = HigherLowerGameStatus.Won;
                    await FinishGameAsync(game);
                }
                else
                {
                    await SaveGameAsync(game);
                }
            }
            else
            {
                game.Status = HigherLowerGameStatus.Lost;
                game.CurrentPayout = 0;
                await FinishGameAsync(game);
            }

            return (game, isCorrect, newCard);
        }

        public async Task<HigherLowerGame> CashoutAsync(int gameId)
        {
            var game = await GetGameAsync(gameId);
            if (game == null || game.Status != HigherLowerGameStatus.Active)
                return game;

            // Cannot cashout on round 0 (must play at least 1 round)
            if (game.CurrentRound < 1)
                return game;

            game.Status = HigherLowerGameStatus.CashedOut;
            game.UpdatedAt = DateTime.UtcNow;
            await FinishGameAsync(game);

            return game;
        }

        private async Task FinishGameAsync(HigherLowerGame game)
        {
            var env = ServerEnvironment.GetServerEnvironment();
            var usersService = env.ServerManager.UsersService;

            if (game.Status == HigherLowerGameStatus.Won || game.Status == HigherLowerGameStatus.CashedOut)
            {
                long payout = (long)game.CurrentPayout;
                if (payout > 0)
                {
                    await usersService.AddBalanceAsync(game.Identifier, payout);
                }
                
                // Register wager for races
                var user = await usersService.GetUserAsync(game.Identifier);
                if (user != null)
                {
                    var raceName = user.DisplayName ?? user.Username ?? user.Identifier;
                    await env.ServerManager.RaceService.RegisterWagerAsync(user.Identifier, raceName, game.BetAmount);
                }
            }
            else if (game.Status == HigherLowerGameStatus.Lost)
            {
                 // Register wager for races even on loss
                var user = await usersService.GetUserAsync(game.Identifier);
                if (user != null)
                {
                    var raceName = user.DisplayName ?? user.Username ?? user.Identifier;
                    await env.ServerManager.RaceService.RegisterWagerAsync(user.Identifier, raceName, game.BetAmount);
                }
            }

            await SaveGameAsync(game);
        }

        private async Task SaveGameAsync(HigherLowerGame game)
        {
            try
            {
                using (var cmd = new DatabaseCommand())
                {
                    cmd.SetCommand(@"
                        UPDATE higher_lower_games 
                        SET status = @status,
                            current_payout = @current_payout,
                            current_round = @current_round,
                            last_card = @last_card,
                            card_history = @card_history,
                            message_id = @message_id,
                            channel_id = @channel_id,
                            updated_at = @updated_at
                        WHERE id = @id");
                    cmd.AddParameter("id", game.Id);
                    cmd.AddParameter("status", (int)game.Status);
                    cmd.AddParameter("current_payout", game.CurrentPayout);
                    cmd.AddParameter("current_round", game.CurrentRound);
                    cmd.AddParameter("last_card", JsonSerializer.Serialize(game.LastCard));
                    cmd.AddParameter("card_history", JsonSerializer.Serialize(game.CardHistory));
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
                        UPDATE higher_lower_games 
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

        private Card GenerateRandomCard()
        {
            var suits = new[] { "Hearts", "Diamonds", "Clubs", "Spades" };
            var ranks = new[] { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };

            var suit = suits[RandomNumberGenerator.GetInt32(suits.Length)];
            var rank = ranks[RandomNumberGenerator.GetInt32(ranks.Length)];

            return new Card(suit, rank);
        }

        private HigherLowerGame MapGame(System.Data.Common.DbDataReader reader)
        {
            var game = new HigherLowerGame
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                UserId = reader.GetInt32(reader.GetOrdinal("user_id")),
                Identifier = reader.GetString(reader.GetOrdinal("identifier")),
                BetAmount = reader.GetInt64(reader.GetOrdinal("bet_amount")),
                CurrentPayout = reader.GetDecimal(reader.GetOrdinal("current_payout")),
                CurrentRound = reader.GetInt32(reader.GetOrdinal("current_round")),
                MaxRounds = reader.GetInt32(reader.GetOrdinal("max_rounds")),
                Status = (HigherLowerGameStatus)reader.GetInt32(reader.GetOrdinal("status")),
                LastCard = JsonSerializer.Deserialize<Card>(reader.GetString(reader.GetOrdinal("last_card"))),
                CardHistory = JsonSerializer.Deserialize<List<Card>>(reader.GetString(reader.GetOrdinal("card_history"))),
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
