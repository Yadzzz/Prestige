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
        public string Identifier { get; set; }
        public string Sid { get; set; }
        public string AuthToken { get; set; }
    }
}
