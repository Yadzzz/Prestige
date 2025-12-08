using Server.Infrastructure.Database;
using System;
using System.Threading.Tasks;

namespace Server.Client.Users
{
    public class UsersService
    {
        private readonly DatabaseManager _databaseManager;

        public UsersService(DatabaseManager databaseManager)
        {
            _databaseManager = databaseManager;
        }

        public bool AddBalance(string identifier, long amount)
        {
            if (amount <= 0)
            {
                return false;
            }
            var updated = TryUpdateBalance(identifier, amount);

            try
            {
                if (updated)
                {
                    var env = ServerEnvironment.GetServerEnvironment();
                    env.ServerManager.LogsService.Log(
                        source: nameof(UsersService),
                        level: "Info",
                        userIdentifier: identifier,
                        action: "BalanceIncreased",
                        message: $"Balance increased by {amount}K for user={identifier}",
                        exception: null);
                }
            }
            catch
            {
                // ignore logging failures
            }

            return updated;
        }

        public bool RemoveBalance(string identifier, long amount)
        {
            if (amount <= 0)
            {
                return false;
            }
            var updated = TryUpdateBalance(identifier, -amount);

            try
            {
                if (updated)
                {
                    var env = ServerEnvironment.GetServerEnvironment();
                    env.ServerManager.LogsService.Log(
                        source: nameof(UsersService),
                        level: "Info",
                        userIdentifier: identifier,
                        action: "BalanceDecreased",
                        message: $"Balance decreased by {amount}K for user={identifier}",
                        exception: null);
                }
            }
            catch
            {
                // ignore logging failures
            }

            return updated;
        }

        public bool UserExists(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                return false;
            }
            try
            {
                using (var command = new DatabaseCommand())
                {
                    command.SetCommand("SELECT COUNT(*) FROM users WHERE identifier = @identifier");
                    command.AddParameter("identifier", identifier);

                    var result = command.ExecuteQuery();

                    return Convert.ToInt32(result) > 0;
                }
            }
            catch (Exception ex)
            {
                try
                {
                    var env = ServerEnvironment.GetServerEnvironment();
                    env.ServerManager.LoggerManager.LogError($"[UsersService.UserExistsError] identifier={identifier} ex={ex}");
                }
                catch
                {
                    Console.WriteLine($"[UsersService.UserExistsError] {ex}");
                }
            }
            return false;
        }

        public bool TryGetUser(string identifier, out User user)
        {
            user = null;

            if (string.IsNullOrEmpty(identifier))
            {
                return false;
            }

            try
            {
                using (var command = new DatabaseCommand())
                {
                    command.SetCommand("SELECT * FROM users WHERE identifier = @identifier LIMIT 1");
                    command.AddParameter("identifier", identifier);

                    using (var reader = command.ExecuteDataReader())
                    {
                        if (reader == null)
                        {
                            return false;
                        }

                        while (reader.Read())
                        {
                            user = new User
                            {
                                Id = Convert.ToInt32(reader["id"]),
                                Identifier = reader["identifier"].ToString(),
                                Username = reader["username"].ToString(),
                                DisplayName = reader["display_name"].ToString(),
                                Balance = Convert.ToInt64(reader["balance"]),
                                StakeStreak = reader["stake_streak"] == DBNull.Value ? 0 : Convert.ToInt32(reader["stake_streak"]),
                                StakeLoseStreak = reader["stake_lose_streak"] == DBNull.Value ? 0 : Convert.ToInt32(reader["stake_lose_streak"])
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    var env = ServerEnvironment.GetServerEnvironment();
                    env.ServerManager.LoggerManager.LogError($"[UsersService.TryGetUserError] identifier={identifier} ex={ex}");
                }
                catch
                {
                    Console.WriteLine($"[UsersService.TryGetUserError] {ex}");
                }
            }

            return user != null;
        }

        public bool CreateUser(string identifier, string username, string displayName)
        {
            if (string.IsNullOrEmpty(identifier) || string.IsNullOrEmpty(username))
            {
                return false;
            }

            // If the user already exists, treat creation as a no-op success.
            if (UserExists(identifier))
            {
                return true;
            }

            try
            {
                using (var command = new DatabaseCommand())
                {
                    command.SetCommand("INSERT INTO users (identifier, username, display_name, balance, stake_streak, stake_lose_streak) VALUES (@identifier, @username, @display_name, @balance, @stake_streak, @stake_lose_streak)");
                    command.AddParameter("identifier", identifier);
                    command.AddParameter("username", username);
                    command.AddParameter("display_name", displayName);
                    command.AddParameter("balance", 0);
                    command.AddParameter("stake_streak", 0);
                    command.AddParameter("stake_lose_streak", 0);

                    int rowsAffected = command.ExecuteQuery();

                    return rowsAffected > 0;
                }
            }
            catch (Exception ex)
            {
                try
                {
                    var env = ServerEnvironment.GetServerEnvironment();
                    env.ServerManager.LoggerManager.LogError($"[UsersService.CreateUserError] identifier={identifier} username={username} ex={ex}");
                }
                catch
                {
                    Console.WriteLine($"[UsersService.CreateUserError] {ex}");
                }
                return false;
            }
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
            catch (Exception ex)
            {
                try
                {
                    var env = ServerEnvironment.GetServerEnvironment();
                    env.ServerManager.LoggerManager.LogError($"[UsersService.UpdateBalanceError] identifier={identifier} delta={delta} ex={ex}");
                }
                catch
                {
                    Console.WriteLine($"[UsersService.UpdateBalanceError] {ex}");
                }
                return false;
            }
        }

        public Task<User?> EnsureUserAsync(string userId, string username, string displayName)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return Task.FromResult<User?>(null);
            }

            if (!TryGetUser(userId, out var user) || user == null)
            {
                // User does not exist yet; try to create and then fetch.
                if (!CreateUser(userId, username, displayName))
                {
                    return Task.FromResult<User?>(null);
                }

                if (!TryGetUser(userId, out user) || user == null)
                {
                    return Task.FromResult<User?>(null);
                }
            }

            return Task.FromResult<User?>(user);
        }
    }
}
