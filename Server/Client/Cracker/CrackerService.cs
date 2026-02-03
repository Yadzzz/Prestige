using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Server.Client.Users;
using Server.Infrastructure;
using Server.Infrastructure.Database;

namespace Server.Client.Cracker
{
    public class CrackerService
    {
        private readonly DatabaseManager _databaseManager;
        private const double HouseEdge = 0.97;
        
        public static readonly List<string> AllHats = new List<string> 
        { 
            "Red", "Yellow", "Green", "Blue", "Purple", "White" 
        };

        public CrackerService(DatabaseManager databaseManager)
        {
            _databaseManager = databaseManager;
        }

        public async Task<CrackerGame> CreateGameAsync(User user, long betAmount)
        {
            if (user == null || betAmount <= 0)
                return null;

            try
            {
                var game = new CrackerGame
                {
                    UserId = user.Id,
                    Identifier = user.Identifier,
                    BetAmount = betAmount,
                    Status = CrackerGameStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // Save to DB
                using (var cmd = new DatabaseCommand())
                {
                    cmd.SetCommand(@"
                        INSERT INTO cracker_games 
                        (user_id, identifier, bet_amount, status, selected_hats, created_at, updated_at)
                        VALUES (@user_id, @identifier, @bet_amount, @status, @selected_hats, @created_at, @updated_at);
                        SELECT LAST_INSERT_ID();");
                    cmd.AddParameter("user_id", game.UserId);
                    cmd.AddParameter("identifier", game.Identifier);
                    cmd.AddParameter("bet_amount", game.BetAmount);
                    cmd.AddParameter("status", (int)game.Status);
                    cmd.AddParameter("selected_hats", JsonSerializer.Serialize(game.SelectedHats));
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

        public async Task<CrackerGame> GetGameAsync(int gameId)
        {
            try
            {
                using (var cmd = new DatabaseCommand())
                {
                    cmd.SetCommand("SELECT * FROM cracker_games WHERE id = @id LIMIT 1");
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

        public async Task UpdateGameAsync(CrackerGame game)
        {
            try
            {
                game.UpdatedAt = DateTime.UtcNow;
                using (var cmd = new DatabaseCommand())
                {
                    cmd.SetCommand(@"
                        UPDATE cracker_games 
                        SET status = @status, 
                            selected_hats = @selected_hats, 
                            result_hat = @result_hat, 
                            multiplier = @multiplier, 
                            payout = @payout, 
                            message_id = @message_id, 
                            updated_at = @updated_at
                        WHERE id = @id");
                    
                    cmd.AddParameter("status", (int)game.Status);
                    cmd.AddParameter("selected_hats", JsonSerializer.Serialize(game.SelectedHats));
                    cmd.AddParameter("result_hat", game.ResultHat);
                    cmd.AddParameter("multiplier", game.Multiplier);
                    cmd.AddParameter("payout", game.Payout);
                    cmd.AddParameter("message_id", game.MessageId);
                    cmd.AddParameter("updated_at", game.UpdatedAt);
                    cmd.AddParameter("id", game.Id);

                    await cmd.ExecuteQueryAsync();
                }
            }
            catch (Exception ex)
            {
                var env = ServerEnvironment.GetServerEnvironment();
                env.ServerManager.LoggerManager.LogError($"UpdateGameAsync failed: {ex}");
            }
        }

        private CrackerGame MapGame(System.Data.IDataReader reader)
        {
            var game = new CrackerGame
            {
                Id = Convert.ToInt32(reader["id"]),
                UserId = Convert.ToInt32(reader["user_id"]),
                Identifier = reader["identifier"].ToString(),
                BetAmount = Convert.ToInt64(reader["bet_amount"]),
                Status = (CrackerGameStatus)Convert.ToInt32(reader["status"]),
                CreatedAt = Convert.ToDateTime(reader["created_at"]),
                UpdatedAt = Convert.ToDateTime(reader["updated_at"])
            };

            if (reader["selected_hats"] != DBNull.Value)
            {
                var json = reader["selected_hats"].ToString();
                game.SelectedHats = JsonSerializer.Deserialize<HashSet<string>>(json) ?? new HashSet<string>();
            }

            if (reader["result_hat"] != DBNull.Value)
                game.ResultHat = reader["result_hat"].ToString();

            if (reader["multiplier"] != DBNull.Value)
                game.Multiplier = Convert.ToDecimal(reader["multiplier"]);

            if (reader["payout"] != DBNull.Value)
                game.Payout = Convert.ToInt64(reader["payout"]);
            
            if (reader["message_id"] != DBNull.Value)
                 game.MessageId = Convert.ToUInt64(reader["message_id"]);

            return game;
        }
        
        public double CalculateMultiplier(int selectedCount)
        {
            if (selectedCount <= 0 || selectedCount >= 6) return 0;
            // baseMultiplier = 6 / k
            // multiplier = round1( (6/k) * HOUSE_EDGE )
            double raw = (6.0 / selectedCount) * HouseEdge;
            return Math.Round(raw, 1);
        }

        public string GetHatEmoji(string color)
        {
            return color switch
            {
                "Red" => "👑", 
                _ => "👑"
            };
        }

        public async Task<bool> ToggleHatAsync(CrackerGame game, string color)
        {
            if (game.Status != CrackerGameStatus.Active) return false;
            
            if (game.SelectedHats.Contains(color))
                game.SelectedHats.Remove(color);
            else
                game.SelectedHats.Add(color);
                
            await UpdateGameAsync(game);
            return true;
        }

        public async Task PullAsync(CrackerGame game, User user)
        {
             if (game.Status != CrackerGameStatus.Active) return;
             if (game.SelectedHats.Count == 0) return;

             // Roll 1 of 6 hats
             var winningIndex = Random.Shared.Next(AllHats.Count);
             var resultHat = AllHats[winningIndex];
             game.ResultHat = resultHat;

             // Check win
             bool win = game.SelectedHats.Contains(resultHat);
             
             // Calculate multiplier
             double multiplier = CalculateMultiplier(game.SelectedHats.Count);
             
             if (win)
             {
                 game.Multiplier = (decimal)multiplier;
                 game.Payout = (long)(game.BetAmount * multiplier);
             }
             else
             {
                 game.Multiplier = 0;
                 game.Payout = 0;
             }

             game.Status = CrackerGameStatus.Finished;
             
             // Update User Balance
             // For win: User balance += payout. 
             // Note: Bet was already deducted (or locked). 
             // If bet was deducted, we just add payout.
             // Usually system deducts bet on creation (lock).
             
             var env = ServerEnvironment.GetServerEnvironment();
             var usersService = env.ServerManager.UsersService;
             
             if (win)
             {
                 await usersService.AddBalanceAsync(game.Identifier, game.Payout);
                 // Need to refresh user object if needed outside
             }
             
             await UpdateGameAsync(game);

             // Logging
             env.ServerManager.LiveFeedService?.PublishCracker(win ? game.Payout : game.BetAmount, win, game.Multiplier);
             
             var raceName = user.DisplayName ?? user.Username ?? user.Identifier;
             await env.ServerManager.RaceService.RegisterWagerAsync(game.Identifier, raceName, game.BetAmount);
        }

        public async Task CancelGameAsync(CrackerGame game)
        {
             if (game.Status != CrackerGameStatus.Active) return;
             
             game.Status = CrackerGameStatus.Cancelled;
             
             // Refund
             var env = ServerEnvironment.GetServerEnvironment();
             var usersService = env.ServerManager.UsersService;
             await usersService.AddBalanceAsync(game.Identifier, game.BetAmount);
             
             await UpdateGameAsync(game);
        }
    }
}
