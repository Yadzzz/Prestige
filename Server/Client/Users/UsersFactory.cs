using DSharpPlus.SlashCommands;
using Server.Infrastructure.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Server.Client.Users
{
    public static class UsersFactory
    {
        public static bool UserExists(string identifier)
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
                //Log to file
            }
            return false;
        }


        public static bool TryGetUser(string identifier, out User user)
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
            catch (Exception ex)
            {
                //Log to file
            }

            return user != null;
        }

        public static bool CreateUser(string identifier, string username, string displayName)
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
            catch (Exception ex)
            {
                //Log to file
                return false;
            }
        }

        public static async Task<User?> EnsureUserAsync(string userId, string username, string displayName)
        {
            //if (ctx.User.IsBot || (ctx.User.IsSystem.HasValue && ctx.User.IsSystem.Value))
            //{
            //    await ctx.CreateResponseAsync("Bots and system users cannot use this command.");
            //    return null;
            //}

            if (!UserExists(userId))
                CreateUser(userId, username, displayName);

            if (!TryGetUser(userId, out var user) || user == null)
            {
                return null;
            }

            return user;
        }
    }
}
