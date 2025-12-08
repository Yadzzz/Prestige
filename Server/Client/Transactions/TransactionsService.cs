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

        public async Task<Transaction> CreateDepositRequestAsync(User user, long amountK)
        {
            if (user == null || amountK <= 0)
            {
                return null;
            }

            try
            {
                using (var command = new DatabaseCommand())
                {
                    command.SetCommand("INSERT INTO transactions (user_id, identifier, amount_k, fee_k, type, status, created_at, updated_at) VALUES (@user_id, @identifier, @amount_k, @fee_k, @type, @status, @created_at, @updated_at)");
                    command.AddParameter("user_id", user.Id);
                    command.AddParameter("identifier", user.Identifier);
                    command.AddParameter("amount_k", amountK);
                    command.AddParameter("fee_k", 0L);
                    command.AddParameter("type", (int)TransactionType.Deposit);
                    command.AddParameter("status", (int)TransactionStatus.Pending);
                    command.AddParameter("created_at", DateTime.UtcNow);
                    command.AddParameter("updated_at", DateTime.UtcNow);

                    var rows = await command.ExecuteQueryAsync();
                    if (rows <= 0)
                    {
                        var env = ServerEnvironment.GetServerEnvironment();
                        env.ServerManager.LogsService.Log(
                            source: nameof(TransactionsService),
                            level: "Warning",
                            userIdentifier: user.Identifier,
                            action: "CreateDepositNoRows",
                            message: $"Insert returned 0 rows for deposit user={user.Identifier} amountK={amountK}",
                            exception: null);
                        return null;
                    }
                }

                // fetch last inserted transaction for this user (simpler for now)
                Transaction? createdTx = null;
                using (var fetch = new DatabaseCommand())
                {
                    fetch.SetCommand("SELECT * FROM transactions WHERE user_id = @user_id ORDER BY id DESC LIMIT 1");
                    fetch.AddParameter("user_id", user.Id);

                    using (var reader = await fetch.ExecuteDataReaderAsync())
                    {
                        if (reader != null && reader.Read())
                        {
                            createdTx = MapTransaction(reader);
                        }
                    }
                }

                if (createdTx != null)
                {
                    var env = ServerEnvironment.GetServerEnvironment();
                    env.ServerManager.LogsService.Log(
                        source: nameof(TransactionsService),
                        level: "Info",
                        userIdentifier: user.Identifier,
                        action: "DepositRequested",
                        message: $"Deposit requested txId={createdTx.Id} user={user.Identifier} amountK={amountK}",
                        exception: null,
                        metadataJson: $"{{\"referenceId\":{createdTx.Id},\"kind\":\"Deposit\",\"amountK\":{amountK}}}");

                    return createdTx;
                }
            }
            catch (Exception ex)
            {
                var env = ServerEnvironment.GetServerEnvironment();
                env.ServerManager.LoggerManager.LogError($"CreateDepositRequest failed: {ex}");
                env.ServerManager.LogsService.Log(
                    source: nameof(TransactionsService),
                    level: "Error",
                    userIdentifier: user?.Identifier,
                    action: "CreateDepositRequestException",
                    message: "Unhandled exception during deposit request creation",
                    exception: ex.ToString());
            }

            return null;
        }

        public Transaction CreateDepositRequest(User user, long amountK)
        {
            return CreateDepositRequestAsync(user, amountK).GetAwaiter().GetResult();
        }

        public async Task<Transaction> CreateWithdrawRequestAsync(User user, long amountK)
        {
            if (user == null || amountK <= 0)
            {
                return null;
            }

            try
            {
                using (var command = new DatabaseCommand())
                {
                    command.SetCommand("INSERT INTO transactions (user_id, identifier, amount_k, fee_k, type, status, created_at, updated_at) VALUES (@user_id, @identifier, @amount_k, @fee_k, @type, @status, @created_at, @updated_at)");
                    command.AddParameter("user_id", user.Id);
                    command.AddParameter("identifier", user.Identifier);
                    command.AddParameter("amount_k", amountK);
                    command.AddParameter("fee_k", 0L);
                    command.AddParameter("type", (int)TransactionType.Withdraw);
                    command.AddParameter("status", (int)TransactionStatus.Pending);
                    command.AddParameter("created_at", DateTime.UtcNow);
                    command.AddParameter("updated_at", DateTime.UtcNow);

                    var rows = await command.ExecuteQueryAsync();
                    if (rows <= 0)
                    {
                        return null;
                    }
                }

                Transaction? createdTx = null;
                using (var fetch = new DatabaseCommand())
                {
                    fetch.SetCommand("SELECT * FROM transactions WHERE user_id = @user_id ORDER BY id DESC LIMIT 1");
                    fetch.AddParameter("user_id", user.Id);

                    using (var reader = await fetch.ExecuteDataReaderAsync())
                    {
                        if (reader != null && reader.Read())
                        {
                            createdTx = MapTransaction(reader);
                        }
                    }
                }

                if (createdTx != null)
                {
                    var env = ServerEnvironment.GetServerEnvironment();
                    env.ServerManager.LogsService.Log(
                        source: nameof(TransactionsService),
                        level: "Info",
                        userIdentifier: user.Identifier,
                        action: "WithdrawRequested",
                        message: $"Withdraw requested txId={createdTx.Id} user={user.Identifier} amountK={amountK}",
                        exception: null,
                        metadataJson: $"{{\"referenceId\":{createdTx.Id},\"kind\":\"Withdraw\",\"amountK\":{amountK}}}");

                    return createdTx;
                }
            }
            catch (Exception ex)
            {
                var env = ServerEnvironment.GetServerEnvironment();
                env.ServerManager.LoggerManager.LogError($"CreateWithdrawRequest failed: {ex}");
                env.ServerManager.LogsService.Log(
                    source: nameof(TransactionsService),
                    level: "Error",
                    userIdentifier: user?.Identifier,
                    action: "CreateWithdrawRequestException",
                    message: "Unhandled exception during withdraw request creation",
                    exception: ex.ToString());
            }

            return null;
        }

        public Transaction CreateWithdrawRequest(User user, long amountK)
        {
            return CreateWithdrawRequestAsync(user, amountK).GetAwaiter().GetResult();
        }

        public async Task<Transaction> GetTransactionByIdAsync(int id)
        {
            try
            {
                using (var command = new DatabaseCommand())
                {
                    command.SetCommand("SELECT * FROM transactions WHERE id = @id LIMIT 1");
                    command.AddParameter("id", id);

                    using (var reader = await command.ExecuteDataReaderAsync())
                    {
                        if (reader != null && reader.Read())
                        {
                            return MapTransaction(reader);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var env = ServerEnvironment.GetServerEnvironment();
                env.ServerManager.LoggerManager.LogError($"GetTransactionById failed: {ex}");
                env.ServerManager.LogsService.Log(
                    source: nameof(TransactionsService),
                    level: "Error",
                    userIdentifier: null,
                    action: "GetTransactionByIdException",
                    message: $"Unhandled exception while fetching transaction id={id}",
                    exception: ex.ToString());
            }

            return null;
        }

        public Transaction GetTransactionById(int id)
        {
            return GetTransactionByIdAsync(id).GetAwaiter().GetResult();
        }

        public async Task<(List<Transaction> Transactions, int TotalCount)> GetTransactionsPageForUserAsync(string identifier, int page, int pageSize)
        {
            var result = new List<Transaction>();
            int totalCount = 0;

            if (string.IsNullOrEmpty(identifier) || page < 1 || pageSize <= 0)
            {
                return (result, totalCount);
            }

            try
            {
                using (var countCommand = new DatabaseCommand())
                {
                    countCommand.SetCommand("SELECT COUNT(*) FROM transactions WHERE identifier = @identifier");
                    countCommand.AddParameter("identifier", identifier);

                    using (var reader = await countCommand.ExecuteDataReaderAsync())
                    {
                        if (reader != null && reader.Read())
                        {
                            totalCount = Convert.ToInt32(reader[0]);
                        }
                    }
                }

                var offset = (page - 1) * pageSize;

                using (var command = new DatabaseCommand())
                {
                    command.SetCommand("SELECT * FROM transactions WHERE identifier = @identifier ORDER BY id DESC LIMIT @limit OFFSET @offset");
                    command.AddParameter("identifier", identifier);
                    command.AddParameter("limit", pageSize);
                    command.AddParameter("offset", offset);

                    using (var reader = await command.ExecuteDataReaderAsync())
                    {
                        while (reader != null && reader.Read())
                        {
                            result.Add(MapTransaction(reader));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var env = ServerEnvironment.GetServerEnvironment();
                env.ServerManager.LoggerManager.LogError($"GetTransactionsPageForUser failed: {ex}");
                env.ServerManager.LogsService.Log(
                    source: nameof(TransactionsService),
                    level: "Error",
                    userIdentifier: identifier,
                    action: "GetTransactionsPageForUserException",
                    message: "Unhandled exception while paging transactions for user",
                    exception: ex.ToString());
            }

            return (result, totalCount);
        }

        public List<Transaction> GetTransactionsPageForUser(string identifier, int page, int pageSize, out int totalCount)
        {
            var result = GetTransactionsPageForUserAsync(identifier, page, pageSize).GetAwaiter().GetResult();
            totalCount = result.TotalCount;
            return result.Transactions;
        }

        public async Task<bool> UpdateTransactionStatusAsync(int id, TransactionStatus status, int? staffId, string staffIdentifier, string notes)
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

                    var rows = await command.ExecuteQueryAsync();
                    return rows > 0;
                }
            }
            catch (Exception ex)
            {
                var env = ServerEnvironment.GetServerEnvironment();
                env.ServerManager.LoggerManager.LogError($"UpdateTransactionStatus failed: {ex}");
                env.ServerManager.LogsService.Log(
                    source: nameof(TransactionsService),
                    level: "Error",
                    userIdentifier: null,
                    action: "UpdateTransactionStatusException",
                    message: $"Unhandled exception while updating status for transaction id={id}",
                    exception: ex.ToString());
            }

            return false;
        }

        public bool UpdateTransactionStatus(int id, TransactionStatus status, int? staffId, string staffIdentifier, string notes)
        {
            return UpdateTransactionStatusAsync(id, status, staffId, staffIdentifier, notes).GetAwaiter().GetResult();
        }

        public async Task<bool> UpdateTransactionFeeAsync(int id, long feeK)
        {
            try
            {
                using (var command = new DatabaseCommand())
                {
                    command.SetCommand("UPDATE transactions SET fee_k = @fee_k, updated_at = @updated_at WHERE id = @id");
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
                env.ServerManager.LoggerManager.LogError($"UpdateTransactionFee failed: {ex}");
                env.ServerManager.LogsService.Log(
                    source: nameof(TransactionsService),
                    level: "Error",
                    userIdentifier: null,
                    action: "UpdateTransactionFeeException",
                    message: $"Unhandled exception while updating fee for transaction id={id}",
                    exception: ex.ToString());
            }

            return false;
        }

        public bool UpdateTransactionFee(int id, long feeK)
        {
            return UpdateTransactionFeeAsync(id, feeK).GetAwaiter().GetResult();
        }

        public async Task<bool> UpdateTransactionMessagesAsync(int id, ulong userMessageId, ulong userChannelId, ulong staffMessageId, ulong staffChannelId)
        {
            try
            {
                using (var command = new DatabaseCommand())
                {
                    command.SetCommand("UPDATE transactions SET user_message_id = @user_message_id, user_channel_id = @user_channel_id, staff_message_id = @staff_message_id, staff_channel_id = @staff_channel_id WHERE id = @id");
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
                env.ServerManager.LoggerManager.LogError($"UpdateTransactionMessages failed: {ex}");
                env.ServerManager.LogsService.Log(
                    source: nameof(TransactionsService),
                    level: "Error",
                    userIdentifier: null,
                    action: "UpdateTransactionMessagesException",
                    message: $"Unhandled exception while updating messages for transaction id={id}",
                    exception: ex.ToString());
            }

            return false;
        }

        public bool UpdateTransactionMessages(int id, ulong userMessageId, ulong userChannelId, ulong staffMessageId, ulong staffChannelId)
        {
            return UpdateTransactionMessagesAsync(id, userMessageId, userChannelId, staffMessageId, staffChannelId).GetAwaiter().GetResult();
        }

        private Transaction MapTransaction(System.Data.IDataRecord reader)
        {
            return new Transaction
            {
                Id = Convert.ToInt32(reader["id"]),
                UserId = Convert.ToInt32(reader["user_id"]),
                Identifier = reader["identifier"].ToString(),
                AmountK = Convert.ToInt64(reader["amount_k"]),
                FeeK = reader["fee_k"] == DBNull.Value ? 0L : Convert.ToInt64(reader["fee_k"]),
                Type = (TransactionType)Convert.ToInt32(reader["type"]),
                Status = (TransactionStatus)Convert.ToInt32(reader["status"]),
                StaffId = reader["staff_id"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["staff_id"]),
                StaffIdentifier = reader["staff_identifier"] == DBNull.Value ? null : reader["staff_identifier"].ToString(),
                UserMessageId = reader["user_message_id"] == DBNull.Value ? (ulong?)null : Convert.ToUInt64(reader["user_message_id"]),
                UserChannelId = reader["user_channel_id"] == DBNull.Value ? (ulong?)null : Convert.ToUInt64(reader["user_channel_id"]),
                StaffMessageId = reader["staff_message_id"] == DBNull.Value ? (ulong?)null : Convert.ToUInt64(reader["staff_message_id"]),
                StaffChannelId = reader["staff_channel_id"] == DBNull.Value ? (ulong?)null : Convert.ToUInt64(reader["staff_channel_id"]),
                CreatedAt = Convert.ToDateTime(reader["created_at"]),
                UpdatedAt = Convert.ToDateTime(reader["updated_at"]),
                Notes = reader["notes"] == DBNull.Value ? null : reader["notes"].ToString()
            };
        }
    }
}
