using System;
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

        public bool RecordAdjustment(User targetUser, string staffIdentifier, BalanceAdjustmentType adjustmentType, long amountK, string source, string reason = null)
        {
            if (targetUser == null || amountK <= 0 || string.IsNullOrEmpty(source))
            {
                return false;
            }

            int? staffId = null;

            if (!string.IsNullOrEmpty(staffIdentifier) &&
                _usersService.TryGetUser(staffIdentifier, out var staffUser) &&
                staffUser != null)
            {
                staffId = staffUser.Id;
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

                    var rows = command.ExecuteQuery();
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
    }
}
