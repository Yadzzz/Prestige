using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Server.Client.Users;
using Server.Infrastructure.Database;

namespace Server.Client.Chest
{
    public class ChestService
    {
        private readonly DatabaseManager _databaseManager;
        private const double HouseEdge = 0.05; // 5%

        public ChestService(DatabaseManager databaseManager)
        {
            _databaseManager = databaseManager;
        }

        public async Task<ChestGame> CreateGameAsync(User user, long betAmountK, ulong channelId)
        {
            if (user == null || betAmountK <= 0)
                return null;

            try
            {
                using (var command = new DatabaseCommand())
                {
                    command.SetCommand(@"
                        INSERT INTO chest_games (user_id, identifier, bet_amount_k, selected_item_ids, status, channel_id, created_at, updated_at) 
                        VALUES (@user_id, @identifier, @bet_amount_k, '', @status, @channel_id, @created_at, @updated_at);
                        SELECT LAST_INSERT_ID();");
                    command.AddParameter("user_id", user.Id);
                    command.AddParameter("identifier", user.Identifier);
                    command.AddParameter("bet_amount_k", betAmountK);
                    command.AddParameter("status", (int)ChestGameStatus.Selection);
                    command.AddParameter("channel_id", channelId);
                    command.AddParameter("created_at", DateTime.UtcNow);
                    command.AddParameter("updated_at", DateTime.UtcNow);

                    var result = await command.ExecuteScalarAsync();
                    var newId = Convert.ToInt32(result);

                    return await GetGameAsync(newId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChestService] Error creating game: {ex.Message}");
                return null;
            }
        }

        public async Task<ChestGame> GetGameAsync(int id)
        {
            using (var command = new DatabaseCommand())
            {
                command.SetCommand("SELECT * FROM chest_games WHERE id = @id");
                command.AddParameter("id", id);

                using (var reader = await command.ExecuteDataReaderAsync())
                {
                    if (reader != null && reader.Read())
                    {
                        return MapGame(reader);
                    }
                }
            }
            return null;
        }

        public async Task<bool> UpdateSelectionAsync(int gameId, List<string> selectedIds, ulong messageId)
        {
            var joined = string.Join(",", selectedIds);
            using (var command = new DatabaseCommand())
            {
                command.SetCommand(@"
                    UPDATE chest_games 
                    SET selected_item_ids = @ids, message_id = @msg_id, updated_at = @now 
                    WHERE id = @id");
                command.AddParameter("ids", joined);
                command.AddParameter("msg_id", messageId);
                command.AddParameter("now", DateTime.UtcNow);
                command.AddParameter("id", gameId);

                return await command.ExecuteQueryAsync() > 0;
            }
        }

        public async Task<bool> CompleteGameAsync(int gameId, bool won, long prizeValueK, ChestGameStatus status)
        {
            using (var command = new DatabaseCommand())
            {
                command.SetCommand(@"
                    UPDATE chest_games 
                    SET won = @won, prize_value_k = @prize, status = @status, updated_at = @now 
                    WHERE id = @id");
                command.AddParameter("won", won);
                command.AddParameter("prize", prizeValueK);
                command.AddParameter("status", (int)status);
                command.AddParameter("now", DateTime.UtcNow);
                command.AddParameter("id", gameId);

                return await command.ExecuteQueryAsync() > 0;
            }
        }

        public async Task<bool> CancelGameAsync(int gameId)
        {
            using (var command = new DatabaseCommand())
            {
                command.SetCommand(@"
                    UPDATE chest_games 
                    SET status = @status, updated_at = @now 
                    WHERE id = @id");
                command.AddParameter("status", (int)ChestGameStatus.Cancelled);
                command.AddParameter("now", DateTime.UtcNow);
                command.AddParameter("id", gameId);

                return await command.ExecuteQueryAsync() > 0;
            }
        }

        public double CalculateWinChance(long betAmountK, long totalPrizeValueK)
        {
            if (totalPrizeValueK <= 0) return 0;
            
            // Formula: Chance = (Bet / Prize) * (1 - HouseEdge)
            // Example: Bet 100, Prize 1000. Raw Chance = 10%. With 5% edge -> 9.5%
            
            double rawChance = (double)betAmountK / totalPrizeValueK;
            double chance = rawChance * (1.0 - HouseEdge);

            // Cap at 95% to always have some risk? Or allow 100% if they pay full price + edge?
            // If they pay 1000 for 1000 item. Chance = 1 * 0.95 = 95%.
            // If they pay 1053 for 1000 item. Chance = 1.053 * 0.95 = 100%.
            // Let's cap at 95% for gambling feel, or just let it be.
            // User asked for "house edge so we somehow have better chance".
            
            if (chance > 0.95) chance = 0.95; 
            if (chance < 0.0001) chance = 0.0001; // Minimum chance

            return chance;
        }

        private ChestGame MapGame(System.Data.IDataReader reader)
        {
            return new ChestGame
            {
                Id = Convert.ToInt32(reader["id"]),
                UserId = Convert.ToInt32(reader["user_id"]),
                Identifier = reader["identifier"].ToString(),
                BetAmountK = Convert.ToInt64(reader["bet_amount_k"]),
                SelectedItemIds = reader["selected_item_ids"]?.ToString() ?? "",
                Won = reader["won"] == DBNull.Value ? null : (bool?)Convert.ToBoolean(reader["won"]),
                PrizeValueK = reader["prize_value_k"] == DBNull.Value ? 0 : Convert.ToInt64(reader["prize_value_k"]),
                Status = (ChestGameStatus)Convert.ToInt32(reader["status"]),
                MessageId = reader["message_id"] == DBNull.Value ? null : (ulong?)Convert.ToUInt64(reader["message_id"]),
                ChannelId = reader["channel_id"] == DBNull.Value ? null : (ulong?)Convert.ToUInt64(reader["channel_id"]),
                CreatedAt = Convert.ToDateTime(reader["created_at"]),
                UpdatedAt = Convert.ToDateTime(reader["updated_at"])
            };
        }
    }
}
