using System;
using System.Collections.Generic;
using System.Data;
using Server.Client.Users;
using Server.Infrastructure.Database;

namespace Server.Client.Transactions
{
    public class BalanceAdjustmentsService
    {
        private readonly DatabaseManager _databaseManager;
        private readonly UsersService _usersService;

        public BalanceAdjustmentsService(DatabaseManager databaseManager, UsersService usersService)
        {
            _databaseManager = databaseManager;
            _usersService = usersService;
        }

        public async Task<(List<BalanceAdjustment> Adjustments, int TotalCount)> GetAdjustmentsPageForUserAsync(string userIdentifier, int page, int pageSize)
        {
            int totalCount = 0;
            var results = new List<BalanceAdjustment>();

            if (string.IsNullOrEmpty(userIdentifier))
                return (results, totalCount);

            try
            {
                // 1. Get total count
                using (var countCmd = new DatabaseCommand())
                {
                    countCmd.SetCommand("SELECT COUNT(*) FROM balance_adjustments WHERE user_identifier = @uid");
                    countCmd.AddParameter("uid", userIdentifier);
                    var countObj = await countCmd.ExecuteScalarAsync();
                    if (countObj != null)
                    {
                        totalCount = Convert.ToInt32(countObj);
                    }
                }

                if (totalCount == 0)
                    return (results, totalCount);

                // 2. Get page data
                int offset = (page - 1) * pageSize;
                if (offset < 0) offset = 0;

                using (var cmd = new DatabaseCommand())
                {
                    cmd.SetCommand("SELECT id, user_id, user_identifier, staff_id, staff_identifier, adjustment_type, amount_k, source, created_at, reason FROM balance_adjustments WHERE user_identifier = @uid ORDER BY id DESC LIMIT @limit OFFSET @offset");
                    cmd.AddParameter("uid", userIdentifier);
                    cmd.AddParameter("limit", pageSize);
                    cmd.AddParameter("offset", offset);

                    var table = await cmd.ExecuteDataTableAsync();
                    foreach (System.Data.DataRow row in table.Rows)
                    {
                        results.Add(new BalanceAdjustment
                        {
                            Id = Convert.ToInt32(row["id"]),
                            UserId = Convert.ToInt32(row["user_id"]),
                            UserIdentifier = row["user_identifier"].ToString(),
                            StaffId = row["staff_id"] == DBNull.Value ? (int?)null : Convert.ToInt32(row["staff_id"]),
                            StaffIdentifier = row["staff_identifier"] == DBNull.Value ? null : row["staff_identifier"].ToString(),
                            AdjustmentType = (BalanceAdjustmentType)Convert.ToInt32(row["adjustment_type"]),
                            AmountK = Convert.ToInt64(row["amount_k"]),
                            Source = row["source"].ToString(),
                            CreatedAt = Convert.ToDateTime(row["created_at"]),
                            Reason = row["reason"] == DBNull.Value ? null : row["reason"].ToString()
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                var env = ServerEnvironment.GetServerEnvironment();
                env.ServerManager.LoggerManager.LogError($"[BalanceAdjustmentsService.GetAdjustmentsPageForUser] user={userIdentifier} ex={ex}");
            }

            return (results, totalCount);
        }

        public List<BalanceAdjustment> GetAdjustmentsPageForUser(string userIdentifier, int page, int pageSize, out int totalCount)
        {
            var result = GetAdjustmentsPageForUserAsync(userIdentifier, page, pageSize).GetAwaiter().GetResult();
            totalCount = result.TotalCount;
            return result.Adjustments;
        }

        public async Task<bool> RecordAdjustmentAsync(User targetUser, string staffIdentifier, BalanceAdjustmentType adjustmentType, long amountK, string source, string reason = null)
        {
            if (targetUser == null || amountK <= 0 || string.IsNullOrEmpty(source))
            {
                return false;
            }

            int? staffId = null;

            if (!string.IsNullOrEmpty(staffIdentifier))
            {
                var staffUser = await _usersService.GetUserAsync(staffIdentifier);
                if (staffUser != null)
                {
                    staffId = staffUser.Id;
                }
            }

            try
            {
                using (var command = new DatabaseCommand())
                {
                    command.SetCommand("INSERT INTO balance_adjustments (user_id, user_identifier, staff_id, staff_identifier, adjustment_type, amount_k, source, created_at, reason) VALUES (@user_id, @user_identifier, @staff_id, @staff_identifier, @adjustment_type, @amount_k, @source, @created_at, @reason)");

                    command.AddParameter("user_id", targetUser.Id);
                    command.AddParameter("user_identifier", targetUser.Identifier);
                    command.AddParameter("staff_id", (object)staffId ?? DBNull.Value);
                    command.AddParameter("staff_identifier", (object)(staffIdentifier ?? (object)DBNull.Value));
                    command.AddParameter("adjustment_type", (int)adjustmentType);
                    command.AddParameter("amount_k", amountK);
                    command.AddParameter("source", source);
                    command.AddParameter("created_at", DateTime.UtcNow);
                    command.AddParameter("reason", (object)(reason ?? (object)DBNull.Value));

                    var rows = await command.ExecuteQueryAsync();
                    return rows > 0;
                }
            }
            catch (Exception ex)
            {
                try
                {
                    var env = ServerEnvironment.GetServerEnvironment();
                    env.ServerManager.LoggerManager.LogError($"[BalanceAdjustmentsService.RecordAdjustmentError] user={targetUser.Identifier} staff={staffIdentifier} type={adjustmentType} amountK={amountK} source={source} ex={ex}");
                }
                catch
                {
                    Console.WriteLine($"[BalanceAdjustmentsService.RecordAdjustmentError] {ex}");
                }
            }

            return false;
        }

        public bool RecordAdjustment(User targetUser, string staffIdentifier, BalanceAdjustmentType adjustmentType, long amountK, string source, string reason = null)
        {
            return RecordAdjustmentAsync(targetUser, staffIdentifier, adjustmentType, amountK, source, reason).GetAwaiter().GetResult();
        }
    }
}
