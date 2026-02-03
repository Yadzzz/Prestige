using System;
using Server.Client.Users;
using Server.Infrastructure.Database;

namespace Server.Client.Coinflips
{
    public class CoinflipsService
    {
        private readonly DatabaseManager _databaseManager;

        public CoinflipsService(DatabaseManager databaseManager)
        {
            _databaseManager = databaseManager;
        }

        public async Task<Coinflip> CreateCoinflipAsync(User user, long amountK)
        {
            if (user == null || amountK <= 0)
                return null;

            try
            {
                using (var command = new DatabaseCommand())
                {
                    command.SetCommand(@"
                        INSERT INTO coinflips (user_id, identifier, amount_k, status, created_at, updated_at) 
                        VALUES (@user_id, @identifier, @amount_k, @status, @created_at, @updated_at);
                        SELECT LAST_INSERT_ID();");
                    command.AddParameter("user_id", user.Id);
                    command.AddParameter("identifier", user.Identifier);
                    command.AddParameter("amount_k", amountK);
                    command.AddParameter("status", (int)CoinflipStatus.Pending);
                    command.AddParameter("created_at", DateTime.UtcNow);
                    command.AddParameter("updated_at", DateTime.UtcNow);

                    var result = await command.ExecuteScalarAsync();
                    var newId = Convert.ToInt32(result);

                    // Fetch the created coinflip by ID directly
                    using (var fetch = new DatabaseCommand())
                    {
                        fetch.SetCommand("SELECT * FROM coinflips WHERE id = @id");
                        fetch.AddParameter("id", newId);

                        using (var reader = await fetch.ExecuteDataReaderAsync())
                        {
                            if (reader != null && reader.Read())
                            {
                                return MapCoinflip(reader);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var env = ServerEnvironment.GetServerEnvironment();
                env.ServerManager.LoggerManager.LogError($"CreateCoinflip failed: {ex}");
                env.ServerManager.LogsService.Log(
                    source: nameof(CoinflipsService),
                    level: "Error",
                    userIdentifier: user?.Identifier,
                    action: "CreateCoinflipException",
                    message: "Unhandled exception during coinflip creation",
                    exception: ex.ToString());
            }

            return null;
        }

        // public Coinflip CreateCoinflip(User user, long amountK)
        // {
        //     return CreateCoinflipAsync(user, amountK).GetAwaiter().GetResult();
        // }

        public async Task<Coinflip> GetCoinflipByIdAsync(int id)
        {
            try
            {
                using (var command = new DatabaseCommand())
                {
                    command.SetCommand("SELECT * FROM coinflips WHERE id = @id LIMIT 1");
                    command.AddParameter("id", id);

                    using (var reader = await command.ExecuteDataReaderAsync())
                    {
                        if (reader != null && reader.Read())
                        {
                            return MapCoinflip(reader);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var env = ServerEnvironment.GetServerEnvironment();
                env.ServerManager.LoggerManager.LogError($"GetCoinflipById failed: {ex}");
                env.ServerManager.LogsService.Log(
                    source: nameof(CoinflipsService),
                    level: "Error",
                    userIdentifier: null,
                    action: "GetCoinflipByIdException",
                    message: $"Unhandled exception while fetching coinflip id={id}",
                    exception: ex.ToString());
            }

            return null;
        }

        // public Coinflip GetCoinflipById(int id)
        // {
        //     return GetCoinflipByIdAsync(id).GetAwaiter().GetResult();
        // }

        public async Task<bool> UpdateCoinflipOutcomeAsync(int id, bool choseHeads, bool resultHeads, CoinflipStatus status, ulong messageId, ulong channelId, CoinflipStatus? expectedStatus = null)
        {
            try
            {
                using (var command = new DatabaseCommand())
                {
                    var sql = "UPDATE coinflips SET chose_heads = @chose_heads, result_heads = @result_heads, status = @status, message_id = @message_id, channel_id = @channel_id, updated_at = @updated_at WHERE id = @id";
                    
                    if (expectedStatus.HasValue)
                    {
                        sql += " AND status = @expected_status";
                        command.AddParameter("expected_status", (int)expectedStatus.Value);
                    }

                    command.SetCommand(sql);
                    command.AddParameter("chose_heads", choseHeads);
                    command.AddParameter("result_heads", resultHeads);
                    command.AddParameter("status", (int)status);
                    command.AddParameter("message_id", (long)messageId);
                    command.AddParameter("channel_id", (long)channelId);
                    command.AddParameter("updated_at", DateTime.UtcNow);
                    command.AddParameter("id", id);

                    var rows = await command.ExecuteQueryAsync();
                    return rows > 0;
                }
            }
            catch (Exception ex)
            {
                var env = ServerEnvironment.GetServerEnvironment();
                env.ServerManager.LoggerManager.LogError($"UpdateCoinflipOutcome failed: {ex}");
                env.ServerManager.LogsService.Log(
                    source: nameof(CoinflipsService),
                    level: "Error",
                    userIdentifier: null,
                    action: "UpdateCoinflipOutcomeException",
                    message: $"Unhandled exception while updating outcome for coinflip id={id}",
                    exception: ex.ToString());
            }

            return false;
        }

        // public bool UpdateCoinflipOutcome(int id, bool choseHeads, bool resultHeads, CoinflipStatus status, ulong messageId, ulong channelId)
        // {
        //     return UpdateCoinflipOutcomeAsync(id, choseHeads, resultHeads, status, messageId, channelId).GetAwaiter().GetResult();
        // }

        public async Task<System.Collections.Generic.List<Coinflip>> GetPendingCoinflipsByUserIdAsync(int userId)
        {
            var list = new System.Collections.Generic.List<Coinflip>();
            try
            {
                using (var command = new DatabaseCommand())
                {
                    command.SetCommand("SELECT * FROM coinflips WHERE user_id = @user_id AND status = @status");
                    command.AddParameter("user_id", userId);
                    command.AddParameter("status", (int)CoinflipStatus.Pending);

                    using (var reader = await command.ExecuteDataReaderAsync())
                    {
                        while (reader != null && reader.Read())
                        {
                            list.Add(MapCoinflip(reader));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var env = ServerEnvironment.GetServerEnvironment();
                env.ServerManager.LoggerManager.LogError($"GetPendingCoinflipsByUserId failed: {ex}");
            }
            return list;
        }

        // public System.Collections.Generic.List<Coinflip> GetPendingCoinflipsByUserId(int userId)
        // {
        //     return GetPendingCoinflipsByUserIdAsync(userId).GetAwaiter().GetResult();
        // }

        private Coinflip MapCoinflip(System.Data.IDataRecord reader)
        {
            return new Coinflip
            {
                Id = Convert.ToInt32(reader["id"]),
                UserId = Convert.ToInt32(reader["user_id"]),
                Identifier = reader["identifier"].ToString(),
                AmountK = Convert.ToInt64(reader["amount_k"]),
                ChoseHeads = reader["chose_heads"] == DBNull.Value ? (bool?)null : Convert.ToBoolean(reader["chose_heads"]),
                ResultHeads = reader["result_heads"] == DBNull.Value ? (bool?)null : Convert.ToBoolean(reader["result_heads"]),
                Status = (CoinflipStatus)Convert.ToInt32(reader["status"]),
                MessageId = reader["message_id"] == DBNull.Value ? (ulong?)null : Convert.ToUInt64(reader["message_id"]),
                ChannelId = reader["channel_id"] == DBNull.Value ? (ulong?)null : Convert.ToUInt64(reader["channel_id"]),
                CreatedAt = Convert.ToDateTime(reader["created_at"]),
                UpdatedAt = Convert.ToDateTime(reader["updated_at"])
            };
        }
    }
}
