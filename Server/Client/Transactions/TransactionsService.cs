using Server.Client.Users;
using Server.Infrastructure.Database;
using System;
using System.Collections.Generic;

namespace Server.Client.Transactions
{
    public class TransactionsService
    {
        private readonly DatabaseManager _databaseManager;
        private readonly UsersService _usersService;

        public TransactionsService(DatabaseManager databaseManager, UsersService usersService)
        {
            _databaseManager = databaseManager;
            _usersService = usersService;
        }

        public Transaction CreateDepositRequest(User user, long amountK)
        {
            if (user == null || amountK <= 0)
            {
                return null;
            }

            try
            {
                using (var command = new DatabaseCommand())
                {
                    command.SetCommand("INSERT INTO transactions (user_id, identifier, amount_k, type, status, created_at, updated_at) VALUES (@user_id, @identifier, @amount_k, @type, @status, @created_at, @updated_at)");
                    command.AddParameter("user_id", user.Id);
                    command.AddParameter("identifier", user.Identifier);
                    command.AddParameter("amount_k", amountK);
                    command.AddParameter("type", (int)TransactionType.Deposit);
                    command.AddParameter("status", (int)TransactionStatus.Pending);
                    command.AddParameter("created_at", DateTime.UtcNow);
                    command.AddParameter("updated_at", DateTime.UtcNow);

                    var rows = command.ExecuteQuery();
                    if (rows <= 0)
                    {
                        return null;
                    }
                }

                // fetch last inserted transaction for this user (simpler for now)
                using (var fetch = new DatabaseCommand())
                {
                    fetch.SetCommand("SELECT * FROM transactions WHERE user_id = @user_id ORDER BY id DESC LIMIT 1");
                    fetch.AddParameter("user_id", user.Id);

                    using (var reader = fetch.ExecuteDataReader())
                    {
                        if (reader != null && reader.Read())
                        {
                            return MapTransaction(reader);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // TODO: log error
            }

            return null;
        }

        public Transaction GetTransactionById(int id)
        {
            try
            {
                using (var command = new DatabaseCommand())
                {
                    command.SetCommand("SELECT * FROM transactions WHERE id = @id LIMIT 1");
                    command.AddParameter("id", id);

                    using (var reader = command.ExecuteDataReader())
                    {
                        if (reader != null && reader.Read())
                        {
                            return MapTransaction(reader);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // TODO: log error
            }

            return null;
        }

        public bool UpdateTransactionStatus(int id, TransactionStatus status, int? staffId, string staffIdentifier, string notes)
        {
            try
            {
                using (var command = new DatabaseCommand())
                {
                    command.SetCommand("UPDATE transactions SET status = @status, staff_id = @staff_id, staff_identifier = @staff_identifier, notes = @notes, updated_at = @updated_at WHERE id = @id");
                    command.AddParameter("status", (int)status);
                    command.AddParameter("staff_id", staffId.HasValue ? (object)staffId.Value : DBNull.Value);
                    command.AddParameter("staff_identifier", (object)(staffIdentifier ?? (object)DBNull.Value));
                    command.AddParameter("notes", (object)(notes ?? (object)DBNull.Value));
                    command.AddParameter("updated_at", DateTime.UtcNow);
                    command.AddParameter("id", id);

                    var rows = command.ExecuteQuery();
                    return rows > 0;
                }
            }
            catch (Exception)
            {
                // TODO: log error
            }

            return false;
        }

        private Transaction MapTransaction(System.Data.IDataRecord reader)
        {
            return new Transaction
            {
                Id = Convert.ToInt32(reader["id"]),
                UserId = Convert.ToInt32(reader["user_id"]),
                Identifier = reader["identifier"].ToString(),
                AmountK = Convert.ToInt64(reader["amount_k"]),
                Type = (TransactionType)Convert.ToInt32(reader["type"]),
                Status = (TransactionStatus)Convert.ToInt32(reader["status"]),
                StaffId = reader["staff_id"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["staff_id"]),
                StaffIdentifier = reader["staff_identifier"] == DBNull.Value ? null : reader["staff_identifier"].ToString(),
                CreatedAt = Convert.ToDateTime(reader["created_at"]),
                UpdatedAt = Convert.ToDateTime(reader["updated_at"]),
                Notes = reader["notes"] == DBNull.Value ? null : reader["notes"].ToString()
            };
        }
    }
}
