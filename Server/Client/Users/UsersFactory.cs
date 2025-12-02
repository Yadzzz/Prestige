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
        public static bool TryGetUser(string sid, string authToken, out User user)
        {
            user = null;

            if (string.IsNullOrEmpty(sid) || string.IsNullOrEmpty(authToken))
            {
                return false;
            }

            try
            {
                using (var command = new DatabaseCommand())
                {
                    command.SetCommand("SELECT * FROM users WHERE sid = @sid && auth_token = @auth LIMIT 1");
                    command.AddParameter("sid", sid);
                    command.AddParameter("auth", authToken);

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
                                Sid = sid,
                                AuthToken = authToken
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
    }
}
