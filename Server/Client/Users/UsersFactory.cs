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
            catch (Exception)
            {
                // TODO: log error
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
                                Balance = Convert.ToInt64(reader["balance"])
                            };
                        }
                    }
                }
            }
            catch (Exception)
            {
                // TODO: log error
            }

            return user != null;
        }

        public bool CreateUser(string identifier, string username, string displayName)
        {
            if (string.IsNullOrEmpty(identifier) || string.IsNullOrEmpty(username))
            {
                return false;
            }
            try
            {
                using (var command = new DatabaseCommand())
                {
                    command.SetCommand("INSERT INTO users (identifier, username, display_name, balance) VALUES (@identifier, @username, @display_name, @balance)");
                    command.AddParameter("identifier", identifier);
                    command.AddParameter("username", username);
                    command.AddParameter("display_name", displayName);
                    command.AddParameter("balance", 0);

                    int rowsAffected = command.ExecuteQuery();

                    return rowsAffected > 0;
                }
            }
            catch (Exception)
            {
                // TODO: log error
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
            catch (Exception)
            {
                // TODO: log error
                return false;
            }
        }

        public Task<User?> EnsureUserAsync(string userId, string username, string displayName)
        {
            if (!UserExists(userId))
            {
                CreateUser(userId, username, displayName);
            }

            if (!TryGetUser(userId, out var user) || user == null)
            {
                return Task.FromResult<User?>(null);
            }

            return Task.FromResult<User?>(user);
        }
    }
}
