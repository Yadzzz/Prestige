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

        public Stake CreateStake(User user, long amountK)
        {
            if (user == null || amountK <= 0)
                return null;

            try
            {
                using (var command = new DatabaseCommand())
                {
                    command.SetCommand("INSERT INTO stakes (user_id, identifier, amount_k, status, created_at, updated_at) VALUES (@user_id, @identifier, @amount_k, @status, @created_at, @updated_at)");
                    command.AddParameter("user_id", user.Id);
                    command.AddParameter("identifier", user.Identifier);
                    command.AddParameter("amount_k", amountK);
                    command.AddParameter("status", (int)StakeStatus.Pending);
                    command.AddParameter("created_at", DateTime.UtcNow);
                    command.AddParameter("updated_at", DateTime.UtcNow);

                    var rows = command.ExecuteQuery();
                    if (rows <= 0)
                        return null;
                }

                using (var fetch = new DatabaseCommand())
                {
                    fetch.SetCommand("SELECT * FROM stakes WHERE user_id = @user_id ORDER BY id DESC LIMIT 1");
                    fetch.AddParameter("user_id", user.Id);

                    using (var reader = fetch.ExecuteDataReader())
                    {
                        if (reader != null && reader.Read())
                        {
                            return MapStake(reader);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // TODO: logging
            }

            return null;
        }

        public Stake GetStakeById(int id)
        {
            try
            {
                using (var command = new DatabaseCommand())
                {
                    command.SetCommand("SELECT * FROM stakes WHERE id = @id LIMIT 1");
                    command.AddParameter("id", id);

                    using (var reader = command.ExecuteDataReader())
                    {
                        if (reader != null && reader.Read())
                        {
                            return MapStake(reader);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        public bool UpdateStakeStatus(int id, StakeStatus status)
        {
            try
            {
                using (var command = new DatabaseCommand())
                {
                    command.SetCommand("UPDATE stakes SET status = @status, updated_at = @updated_at WHERE id = @id");
                    command.AddParameter("status", (int)status);
                    command.AddParameter("updated_at", DateTime.UtcNow);
                    command.AddParameter("id", id);

                    var rows = command.ExecuteQuery();
                    return rows > 0;
                }
            }
            catch (Exception)
            {
            }

            return false;
        }

        public bool UpdateStakeMessages(int id, ulong userMessageId, ulong userChannelId, ulong staffMessageId, ulong staffChannelId)
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

                    var rows = command.ExecuteQuery();
                    return rows > 0;
                }
            }
            catch (Exception)
            {
            }

            return false;
        }

        private Stake MapStake(System.Data.IDataRecord reader)
        {
            return new Stake
            {
                Id = Convert.ToInt32(reader["id"]),
                UserId = Convert.ToInt32(reader["user_id"]),
                Identifier = reader["identifier"].ToString(),
                AmountK = Convert.ToInt64(reader["amount_k"]),
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
