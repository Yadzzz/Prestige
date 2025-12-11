using System;
using System.Collections.Generic;
using Server.Client.Users;
using Server.Infrastructure.Database;

namespace Server.Client.Stakes
{
    public class StakesService
    {
        private readonly DatabaseManager _databaseManager;

        public StakesService(DatabaseManager databaseManager)
        {
            _databaseManager = databaseManager;
        }

        public async Task<Stake> CreateStakeAsync(User user, long amountK)
        {
            if (user == null || amountK <= 0)
                return null;

            try
            {
                using (var command = new DatabaseCommand())
                {
                    command.SetCommand(@"
                        INSERT INTO stakes (user_id, identifier, amount_k, fee_k, status, created_at, updated_at) 
                        VALUES (@user_id, @identifier, @amount_k, @fee_k, @status, @created_at, @updated_at);
                        SELECT LAST_INSERT_ID();");
                    command.AddParameter("user_id", user.Id);
                    command.AddParameter("identifier", user.Identifier);
                    command.AddParameter("amount_k", amountK);
                    command.AddParameter("fee_k", 0L);
                    command.AddParameter("status", (int)StakeStatus.Pending);
                    command.AddParameter("created_at", DateTime.UtcNow);
                    command.AddParameter("updated_at", DateTime.UtcNow);

                    var result = await command.ExecuteScalarAsync();
                    var newId = Convert.ToInt32(result);

                    // Fetch the created stake by ID directly
                    using (var fetch = new DatabaseCommand())
                    {
                        fetch.SetCommand("SELECT * FROM stakes WHERE id = @id");
                        fetch.AddParameter("id", newId);

                        using (var reader = await fetch.ExecuteDataReaderAsync())
                        {
                            if (reader != null && reader.Read())
                            {
                                return MapStake(reader);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var env = ServerEnvironment.GetServerEnvironment();
                env.ServerManager.LoggerManager.LogError($"CreateStake failed: {ex}");
                env.ServerManager.LogsService.Log(
                    source: nameof(StakesService),
                    level: "Error",
                    userIdentifier: user?.Identifier,
                    action: "CreateStakeException",
                    message: "Unhandled exception during stake creation",
                    exception: ex.ToString());
            }

            return null;
        }

        // public Stake CreateStake(User user, long amountK)
        // {
        //     return CreateStakeAsync(user, amountK).GetAwaiter().GetResult();
        // }

        public async Task<Stake> GetStakeByIdAsync(int id)
        {
            try
            {
                using (var command = new DatabaseCommand())
                {
                    command.SetCommand("SELECT * FROM stakes WHERE id = @id LIMIT 1");
                    command.AddParameter("id", id);

                    using (var reader = await command.ExecuteDataReaderAsync())
                    {
                        if (reader != null && reader.Read())
                        {
                            return MapStake(reader);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var env = ServerEnvironment.GetServerEnvironment();
                env.ServerManager.LoggerManager.LogError($"GetStakeById failed: {ex}");
                env.ServerManager.LogsService.Log(
                    source: nameof(StakesService),
                    level: "Error",
                    userIdentifier: null,
                    action: "GetStakeByIdException",
                    message: $"Unhandled exception while fetching stake id={id}",
                    exception: ex.ToString());
            }

            return null;
        }

        // public Stake GetStakeById(int id)
        // {
        //     return GetStakeByIdAsync(id).GetAwaiter().GetResult();
        // }

        public async Task<bool> UpdateStakeStatusAsync(int id, StakeStatus status)
        {
            try
            {
                using (var command = new DatabaseCommand())
                {
                    command.SetCommand("UPDATE stakes SET status = @status, updated_at = @updated_at WHERE id = @id");
                    command.AddParameter("status", (int)status);
                    command.AddParameter("updated_at", DateTime.UtcNow);
                    command.AddParameter("id", id);

                    var rows = await command.ExecuteQueryAsync();
                    return rows > 0;
                }
            }
            catch (Exception ex)
            {
                var env = ServerEnvironment.GetServerEnvironment();
                env.ServerManager.LoggerManager.LogError($"UpdateStakeStatus failed: {ex}");
                env.ServerManager.LogsService.Log(
                    source: nameof(StakesService),
                    level: "Error",
                    userIdentifier: null,
                    action: "UpdateStakeStatusException",
                    message: $"Unhandled exception while updating status for stake id={id}",
                    exception: ex.ToString());
            }

            return false;
        }

        // public bool UpdateStakeStatus(int id, StakeStatus status)
        // {
        //     return UpdateStakeStatusAsync(id, status).GetAwaiter().GetResult();
        // }

        public async Task<bool> UpdateStakeMessagesAsync(int id, ulong userMessageId, ulong userChannelId, ulong staffMessageId, ulong staffChannelId)
        {
            try
            {
                using (var command = new DatabaseCommand())
                {
                    command.SetCommand("UPDATE stakes SET user_message_id = @user_message_id, user_channel_id = @user_channel_id, staff_message_id = @staff_message_id, staff_channel_id = @staff_channel_id WHERE id = @id");
                    command.AddParameter("user_message_id", (long)userMessageId);
                    command.AddParameter("user_channel_id", (long)userChannelId);
                    command.AddParameter("staff_message_id", (long)staffMessageId);
                    command.AddParameter("staff_channel_id", (long)staffChannelId);
                    command.AddParameter("id", id);

                    var rows = await command.ExecuteQueryAsync();
                    return rows > 0;
                }
            }
            catch (Exception ex)
            {
                var env = ServerEnvironment.GetServerEnvironment();
                env.ServerManager.LoggerManager.LogError($"UpdateStakeMessages failed: {ex}");
                env.ServerManager.LogsService.Log(
                    source: nameof(StakesService),
                    level: "Error",
                    userIdentifier: null,
                    action: "UpdateStakeMessagesException",
                    message: $"Unhandled exception while updating messages for stake id={id}",
                    exception: ex.ToString());
            }

            return false;
        }

        // public bool UpdateStakeMessages(int id, ulong userMessageId, ulong userChannelId, ulong staffMessageId, ulong staffChannelId)
        // {
        //     return UpdateStakeMessagesAsync(id, userMessageId, userChannelId, staffMessageId, staffChannelId).GetAwaiter().GetResult();
        // }

        public async Task<bool> UpdateStakeFeeAsync(int id, long feeK)
        {
            try
            {
                using (var command = new DatabaseCommand())
                {
                    command.SetCommand("UPDATE stakes SET fee_k = @fee_k, updated_at = @updated_at WHERE id = @id");
                    command.AddParameter("fee_k", feeK);
                    command.AddParameter("updated_at", DateTime.UtcNow);
                    command.AddParameter("id", id);

                    var rows = await command.ExecuteQueryAsync();
                    return rows > 0;
                }
            }
            catch (Exception ex)
            {
                var env = ServerEnvironment.GetServerEnvironment();
                env.ServerManager.LoggerManager.LogError($"UpdateStakeFee failed: {ex}");
                env.ServerManager.LogsService.Log(
                    source: nameof(StakesService),
                    level: "Error",
                    userIdentifier: null,
                    action: "UpdateStakeFeeException",
                    message: $"Unhandled exception while updating fee for stake id={id}",
                    exception: ex.ToString());
            }

            return false;
        }

        // public bool UpdateStakeFee(int id, long feeK)
        // {
        //     return UpdateStakeFeeAsync(id, feeK).GetAwaiter().GetResult();
        // }

        public async Task<System.Collections.Generic.List<Stake>> GetPendingStakesByUserIdAsync(int userId)
        {
            var list = new System.Collections.Generic.List<Stake>();
            try
            {
                using (var command = new DatabaseCommand())
                {
                    command.SetCommand("SELECT * FROM stakes WHERE user_id = @user_id AND status = @status");
                    command.AddParameter("user_id", userId);
                    command.AddParameter("status", (int)StakeStatus.Pending);

                    using (var reader = await command.ExecuteDataReaderAsync())
                    {
                        while (reader != null && reader.Read())
                        {
                            list.Add(MapStake(reader));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var env = ServerEnvironment.GetServerEnvironment();
                env.ServerManager.LoggerManager.LogError($"GetPendingStakesByUserId failed: {ex}");
            }
            return list;
        }

        // public System.Collections.Generic.List<Stake> GetPendingStakesByUserId(int userId)
        // {
        //     return GetPendingStakesByUserIdAsync(userId).GetAwaiter().GetResult();
        // }

        private Stake MapStake(System.Data.IDataRecord reader)
        {
            return new Stake
            {
                Id = Convert.ToInt32(reader["id"]),
                UserId = Convert.ToInt32(reader["user_id"]),
                Identifier = reader["identifier"].ToString(),
                AmountK = Convert.ToInt64(reader["amount_k"]),
                FeeK = reader["fee_k"] == DBNull.Value ? 0L : Convert.ToInt64(reader["fee_k"]),
                Status = (StakeStatus)Convert.ToInt32(reader["status"]),
                UserMessageId = reader["user_message_id"] == DBNull.Value ? (ulong?)null : Convert.ToUInt64(reader["user_message_id"]),
                UserChannelId = reader["user_channel_id"] == DBNull.Value ? (ulong?)null : Convert.ToUInt64(reader["user_channel_id"]),
                StaffMessageId = reader["staff_message_id"] == DBNull.Value ? (ulong?)null : Convert.ToUInt64(reader["staff_message_id"]),
                StaffChannelId = reader["staff_channel_id"] == DBNull.Value ? (ulong?)null : Convert.ToUInt64(reader["staff_channel_id"]),
                CreatedAt = Convert.ToDateTime(reader["created_at"]),
                UpdatedAt = Convert.ToDateTime(reader["updated_at"])
            };
        }
    }
}
