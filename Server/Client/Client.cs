using Server.Client.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Client
{
    public class Client
    {
        public User User { get; set; }

        public Client(User user)
        {
            User = user;
        }

        public bool IsAuthenticated(out string error)
        {
            error = string.Empty;

            if (User == null)
            {
                error = "User not Authenticated";
                return false;
            }

            return true;
        }
    }
}
