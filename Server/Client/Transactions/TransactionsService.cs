using Server.Client.Users;
using Server.Infrastructure.Database;
using System;

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

        public bool AddBalance(string identifier, long amount)
        {
            if (amount <= 0)
            {
                return false;
            }

            return TryUpdateBalance(identifier, amount);
        }

        public bool RemoveBalance(string identifier, long amount)
        {
            if (amount <= 0)
            {
                return false;
            }

            return TryUpdateBalance(identifier, -amount);
        }

        private bool TryUpdateBalance(string identifier, long delta)
        {
            if (string.IsNullOrEmpty(identifier) || delta == 0)
            {
                return false;
            }

            try
            {
                using (var command = new DatabaseCommand())
                {
                    command.SetCommand("UPDATE users SET balance = balance + @delta WHERE identifier = @identifier");
                    command.AddParameter("identifier", identifier);
                    command.AddParameter("delta", delta);

                    var result = command.ExecuteQuery();
                    return Convert.ToInt32(result) > 0;
                }
            }
            catch (Exception)
            {
                // TODO: log error
                return false;
            }
        }
    }
}
