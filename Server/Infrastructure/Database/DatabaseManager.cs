using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace Server.Infrastructure.Database
{
    public class DatabaseManager
    {
        public DatabaseManager()
        {
            try
            {
                using (DatabaseCommand databaseCommand = new DatabaseCommand())
                {
                    databaseCommand.SetCommand("SELECT version()");
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
                throw;
            }

            Console.WriteLine("DatabaseManager Initialized ->");
        }

        public DatabaseCommand CreateDatabaseCommand()
        {
            return new DatabaseCommand();
        }
    }
}
