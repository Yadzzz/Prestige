using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace Server.Infrastructure.Database
{
    public abstract class DatabaseConnection
    {
        protected readonly MySqlConnection _mySqlConnection;

        public DatabaseConnection()
        {
             this._mySqlConnection = new MySqlConnection(ServerConfiguration.ConnectionString);
        }

        protected void OpenConnection()
        {
            if(this._mySqlConnection.State == System.Data.ConnectionState.Closed)
            {
                this._mySqlConnection.Open();
            }
        }

        protected async Task OpenConnectionAsync()
        {
            if (this._mySqlConnection.State == System.Data.ConnectionState.Closed)
            {
                await this._mySqlConnection.OpenAsync();
            }
        }

        protected void CloseConnection()
        {
            if(this._mySqlConnection.State != System.Data.ConnectionState.Closed)
            {
                this._mySqlConnection.Close();
            }
        }
    }
}
