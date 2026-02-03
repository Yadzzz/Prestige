using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Client.Users
{
    public class User
    {
        public int Id { get; set; }
        public string Identifier { get; set; } // Represents discord user id
        public string Username { get; set; }
        public string DisplayName { get; set; }
        public long Balance { get; set; }
        public long WagerLock { get; set; }
        public int StakeStreak { get; set; }
        public int StakeLoseStreak { get; set; }
    }
}
