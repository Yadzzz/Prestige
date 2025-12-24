using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Server.Client.Users;
using Server.Infrastructure.Database;

namespace Server.Client.Mines
{
    public class MinesService
    {
        private readonly DatabaseManager _databaseManager;
        private readonly UsersService _usersService;

        public MinesService(DatabaseManager databaseManager, UsersService usersService)
        {
            _databaseManager = databaseManager;
            _usersService = usersService;
        }

        public async Task<MinesGame> RevealTileAsync(int gameId, int tileIndex)
        {
            var game = await GetGameAsync(gameId);
            if (game == null || game.Status != MinesGameStatus.Active) return game;
            if (game.RevealedTiles.Contains(tileIndex)) return game;

            if (game.MineLocations.Contains(tileIndex))
            {
                game.Status = MinesGameStatus.Lost;
                game.RevealedTiles.Add(tileIndex); // Reveal the mine
                try
                {
                    await UpdateGameAsync(game);
                    return game;
                }
                catch
                {
                    return null;
                }
            }

            game.RevealedTiles.Add(tileIndex);
            
            // Check if all safe tiles revealed (Win condition?)
            // Usually mines allows cashout anytime. If all safe tiles revealed, auto cashout?
            // 24 tiles, m mines. Safe tiles = 24 - m.
            // If revealed count == 24 - m, then auto win.
            
            if (game.RevealedTiles.Count == 24 - game.MinesCount)
            {
                return await CashoutAsync(gameId);
            }

            try
            {
                await UpdateGameAsync(game);
                return game;
            }
            catch
            {
                return null;
            }
        }

        public async Task<MinesGame> CashoutAsync(int gameId)
        {
            var game = await GetGameAsync(gameId);
            if (game == null || game.Status != MinesGameStatus.Active) return game;
            if (game.RevealedTiles.Count == 0) return game; // Cannot cashout with 0 hits

            double multiplier = CalculateMultiplier(game.MinesCount, game.RevealedTiles.Count);
            long payout = (long)(game.BetAmount * multiplier);

            game.Status = MinesGameStatus.CashedOut;
            
            try
            {
                await UpdateGameAsync(game);
                await _usersService.AddBalanceAsync(game.Identifier, payout);
                return game;
            }
            catch
            {
                return null;
            }
        }

        public async Task<MinesGame> CancelGameAsync(int gameId)
        {
            var game = await GetGameAsync(gameId);
            if (game == null || game.Status != MinesGameStatus.Active) return game;
            if (game.RevealedTiles.Count > 0) return game; // Cannot cancel if tiles revealed

            game.Status = MinesGameStatus.Cancelled;
            
            try
            {
                await UpdateGameAsync(game);
                await _usersService.AddBalanceAsync(game.Identifier, game.BetAmount);
                return game;
            }
            catch
            {
                return null;
            }
        }

        public async Task<MinesGame> CreateGameAsync(User user, long betAmount, int minesCount)
        {
            if (user == null || betAmount <= 0 || minesCount < 1 || minesCount > 23)
                return null;

            try
            {
                var game = new MinesGame
                {
                    UserId = user.Id,
                    Identifier = user.Identifier,
                    BetAmount = betAmount,
                    MinesCount = minesCount,
                    Status = MinesGameStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    MineLocations = GenerateMines(minesCount),
                    RevealedTiles = new List<int>()
                };

                // Save to DB
                using (var cmd = new DatabaseCommand())
                {
                    cmd.SetCommand(@"
                        INSERT INTO mines_games 
                        (user_id, identifier, bet_amount, mines_count, status, mine_locations, revealed_tiles, created_at, updated_at)
                        VALUES (@user_id, @identifier, @bet_amount, @mines_count, @status, @mine_locations, @revealed_tiles, @created_at, @updated_at);
                        SELECT LAST_INSERT_ID();");
                    cmd.AddParameter("user_id", game.UserId);
                    cmd.AddParameter("identifier", game.Identifier);
                    cmd.AddParameter("bet_amount", game.BetAmount);
                    cmd.AddParameter("mines_count", game.MinesCount);
                    cmd.AddParameter("status", (int)game.Status);
                    cmd.AddParameter("mine_locations", JsonSerializer.Serialize(game.MineLocations));
                    cmd.AddParameter("revealed_tiles", JsonSerializer.Serialize(game.RevealedTiles));
                    cmd.AddParameter("created_at", game.CreatedAt);
                    cmd.AddParameter("updated_at", game.UpdatedAt);

                    var result = await cmd.ExecuteScalarAsync();
                    game.Id = Convert.ToInt32(result);
                }

                return game;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating mines game: {ex.Message}");
                return null;
            }
        }

        public async Task<MinesGame> GetGameAsync(int gameId)
        {
            try
            {
                using (var cmd = new DatabaseCommand())
                {
                    cmd.SetCommand("SELECT * FROM mines_games WHERE id = @id");
                    cmd.AddParameter("id", gameId);
                    var dt = await cmd.ExecuteDataTableAsync();
                    if (dt.Rows.Count == 0) return null;

                    var row = dt.Rows[0];
                    return new MinesGame
                    {
                        Id = Convert.ToInt32(row["id"]),
                        UserId = Convert.ToInt32(row["user_id"]),
                        Identifier = row["identifier"].ToString(),
                        BetAmount = Convert.ToInt64(row["bet_amount"]),
                        MinesCount = Convert.ToInt32(row["mines_count"]),
                        Status = (MinesGameStatus)Convert.ToInt32(row["status"]),
                        MineLocations = JsonSerializer.Deserialize<List<int>>(row["mine_locations"].ToString()),
                        RevealedTiles = JsonSerializer.Deserialize<List<int>>(row["revealed_tiles"].ToString()),
                        CreatedAt = Convert.ToDateTime(row["created_at"]),
                        UpdatedAt = Convert.ToDateTime(row["updated_at"]),
                        MessageId = row["message_id"] != DBNull.Value ? (ulong?)Convert.ToUInt64(row["message_id"]) : null,
                        ChannelId = row["channel_id"] != DBNull.Value ? (ulong?)Convert.ToUInt64(row["channel_id"]) : null
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting mines game: {ex.Message}");
                return null;
            }
        }

        public async Task UpdateGameAsync(MinesGame game)
        {
            game.UpdatedAt = DateTime.UtcNow;
            using (var cmd = new DatabaseCommand())
            {
                cmd.SetCommand(@"
                    UPDATE mines_games 
                    SET status = @status, revealed_tiles = @revealed_tiles, updated_at = @updated_at, message_id = @message_id, channel_id = @channel_id
                    WHERE id = @id");
                cmd.AddParameter("status", (int)game.Status);
                cmd.AddParameter("revealed_tiles", JsonSerializer.Serialize(game.RevealedTiles));
                cmd.AddParameter("updated_at", game.UpdatedAt);
                cmd.AddParameter("message_id", game.MessageId);
                cmd.AddParameter("channel_id", game.ChannelId);
                cmd.AddParameter("id", game.Id);
                
                int rows = await cmd.ExecuteQueryAsync();
                if (rows == 0)
                    throw new Exception("Failed to update game state in database.");
            }
        }

        public double CalculateMultiplier(int minesCount, int hits)
        {
            if (hits == 0) return 1.0;
            
            // P = C(24-mines, hits) / C(24, hits)
            // Multiplier = (1/P) * 0.97
            
            double combinationsTotal = Combinations(24, hits);
            double combinationsSafe = Combinations(24 - minesCount, hits);
            
            if (combinationsSafe == 0) return 0; // Should not happen if logic is correct

            double probability = combinationsSafe / combinationsTotal;
            double fair = 1.0 / probability;
            double multiplier = fair * 0.97;

            // Always round down (truncate) to 2 decimal places? Or just floor?
            // User said "Always round down (never up)". Usually implies floor to 2 decimals or just floor.
            // Let's assume floor to 2 decimal places for currency display, but keep precision for calculation?
            // "paying betAmount x multiplier".
            // Let's just return the raw value, and handle rounding when calculating payout.
            // Actually, standard is usually floor(val * 100) / 100.
            
            return Math.Floor(multiplier * 100) / 100;
        }

        private double Combinations(int n, int k)
        {
            if (k < 0 || k > n) return 0;
            if (k == 0 || k == n) return 1;
            if (k > n / 2) k = n - k;
            
            double res = 1;
            for (int i = 1; i <= k; i++)
            {
                res = res * (n - i + 1) / i;
            }
            return res;
        }

        private List<int> GenerateMines(int count)
        {
            var mines = new HashSet<int>();
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] data = new byte[4];
                while (mines.Count < count)
                {
                    rng.GetBytes(data);
                    int rand = Math.Abs(BitConverter.ToInt32(data, 0)) % 24;
                    mines.Add(rand);
                }
            }
            return mines.ToList();
        }
    }
}
