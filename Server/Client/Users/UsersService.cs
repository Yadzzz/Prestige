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

        public async Task<bool> AddBalanceAsync(string identifier, long amount)
        {
            if (amount <= 0)
            {
                return false;
            }
            var updated = await UpdateBalanceAsync(identifier, amount);

            if (updated)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var env = ServerEnvironment.GetServerEnvironment();
                        await env.ServerManager.LogsService.LogAsync(
                            source: nameof(UsersService),
                            level: "Info",
                            userIdentifier: identifier,
                            action: "BalanceIncreased",
                            message: $"Balance increased by {amount}K for user={identifier}",
                            exception: null);
                    }
                    catch (Exception ex)
                    {
                        var env = ServerEnvironment.GetServerEnvironment();
                        env.ServerManager.LoggerManager.LogError($"[UsersService] Failed to log balance increase for {identifier}: {ex.Message}");
                    }
                });
            }

            return updated;
        }

        // Keep synchronous method for backward compatibility
        public bool AddBalance(string identifier, long amount)
        {
            return AddBalanceAsync(identifier, amount).GetAwaiter().GetResult();
        }

        public async Task<bool> RemoveBalanceAsync(string identifier, long amount)
        {
            if (amount <= 0)
            {
                return false;
            }
            var updated = await UpdateBalanceAsync(identifier, -amount);

            if (updated)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var env = ServerEnvironment.GetServerEnvironment();
                        await env.ServerManager.LogsService.LogAsync(
                            source: nameof(UsersService),
                            level: "Info",
                            userIdentifier: identifier,
                            action: "BalanceDecreased",
                            message: $"Balance decreased by {amount}K for user={identifier}",
                            exception: null);
                    }
                    catch (Exception ex)
                    {
                        var env = ServerEnvironment.GetServerEnvironment();
                        env.ServerManager.LoggerManager.LogError($"[UsersService] Failed to log balance decrease for {identifier}: {ex.Message}");
                    }
                });
            }

            return updated;
        }

        // Keep synchronous method for backward compatibility
        public bool RemoveBalance(string identifier, long amount)
        {
            return RemoveBalanceAsync(identifier, amount).GetAwaiter().GetResult();
        }

        public async Task<bool> UserExistsAsync(string identifier)
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

                    var result = await command.ExecuteScalarAsync();

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

        // Keep synchronous method for backward compatibility
        public bool UserExists(string identifier)
        {
            return UserExistsAsync(identifier).GetAwaiter().GetResult();
        }

        public async Task<User?> GetUserAsync(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                return null;
            }

            try
            {
                using (var command = new DatabaseCommand())
                {
                    command.SetCommand("SELECT * FROM users WHERE identifier = @identifier LIMIT 1");
                    command.AddParameter("identifier", identifier);

                    using (var reader = await command.ExecuteDataReaderAsync())
                    {
                        if (reader == null)
                        {
                            return null;
                        }

                        if (await reader.ReadAsync())
                        {
                            return new User
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
                    env.ServerManager.LoggerManager.LogError($"[UsersService.GetUserError] identifier={identifier} ex={ex}");
                }
                catch
                {
                    Console.WriteLine($"[UsersService.GetUserError] {ex}");
                }
            }

            return null;
        }

        // Keep synchronous method for backward compatibility
        public bool TryGetUser(string identifier, out User user)
        {
            user = GetUserAsync(identifier).GetAwaiter().GetResult();
            return user != null;
        }

        public async Task<bool> CreateUserAsync(string identifier, string username, string displayName)
        {
            if (string.IsNullOrEmpty(identifier) || string.IsNullOrEmpty(username))
            {
                return false;
            }

            // Optimization: Try to insert directly using INSERT IGNORE.
            // This avoids the extra roundtrip to check if user exists.

            try
            {
                using (var command = new DatabaseCommand())
                {
                    command.SetCommand("INSERT IGNORE INTO users (identifier, username, display_name, balance, stake_streak, stake_lose_streak) VALUES (@identifier, @username, @display_name, 0, 0, 0)");
                    command.AddParameter("identifier", identifier);
                    command.AddParameter("username", username);
                    command.AddParameter("display_name", displayName);

                    await command.ExecuteQueryAsync();

                    // With INSERT IGNORE, if it returns 0, it means user exists.
                    // If it returns 1, user was created.
                    // In both cases, we successfully ensured the user exists.
                    return true;
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

        // Keep synchronous method for backward compatibility
        public bool CreateUser(string identifier, string username, string displayName)
        {
            return CreateUserAsync(identifier, username, displayName).GetAwaiter().GetResult();
        }

        private async Task<bool> UpdateBalanceAsync(string identifier, long delta)
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

                    var result = await command.ExecuteQueryAsync();
                    return result > 0;
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

        // Keep synchronous method for backward compatibility
        private bool TryUpdateBalance(string identifier, long delta)
        {
            return UpdateBalanceAsync(identifier, delta).GetAwaiter().GetResult();
        }

        private async Task<User?> CreateAndGetUserAsync(string identifier, string username, string displayName)
        {
            try
            {
                using (var command = new DatabaseCommand())
                {
                    // Combined INSERT IGNORE + SELECT to reduce roundtrips
                    command.SetCommand(@"
                        INSERT IGNORE INTO users (identifier, username, display_name, balance, stake_streak, stake_lose_streak) 
                        VALUES (@identifier, @username, @display_name, 0, 0, 0);
                        SELECT * FROM users WHERE identifier = @identifier LIMIT 1;");
                    
                    command.AddParameter("identifier", identifier);
                    command.AddParameter("username", username);
                    command.AddParameter("display_name", displayName);

                    using (var reader = await command.ExecuteDataReaderAsync())
                    {
                        if (reader != null && await reader.ReadAsync())
                        {
                            return new User
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
                var env = ServerEnvironment.GetServerEnvironment();
                env.ServerManager.LoggerManager.LogError($"[UsersService] CreateAndGetUserAsync failed for {identifier}: {ex.Message}");
            }
            return null;
        }

        public async Task<User?> EnsureUserAsync(string userId, string username, string displayName)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return null;
            }

            var user = await GetUserAsync(userId);
            if (user == null)
            {
                // Optimized creation: Try to create and fetch in one go
                user = await CreateAndGetUserAsync(userId, username, displayName);
            }

            return user;
        }
    }
}
