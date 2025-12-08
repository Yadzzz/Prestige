using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Infrastructure.Database
{
    public class DatabaseCommand : DatabaseConnection, IDisposable
    {
        private readonly MySqlCommand _MySqlCommand;

        public DatabaseCommand()
        {
            this._MySqlCommand = new MySqlCommand();
            this._MySqlCommand.Connection = base._mySqlConnection;
        }

        public DatabaseCommand(string command)
        {
            this._MySqlCommand = new MySqlCommand();
            this._MySqlCommand.Connection = base._mySqlConnection;
            this.SetCommand(command);
        }

        public void SetCommand(string command)
        {
            this._MySqlCommand.CommandText = command;
        }

        public void AddParameter(string name, object value)
        {
            this._MySqlCommand.Parameters.AddWithValue(name, value);
        }

        public int ExecuteQuery()
        {
            if (base._mySqlConnection.State == ConnectionState.Closed)
            {
                base.OpenConnection();
            }

            int rowsAffected = this._MySqlCommand.ExecuteNonQuery();

            return rowsAffected;
        }

        public object ExecuteScalar()
        {
            if (base._mySqlConnection.State == ConnectionState.Closed)
            {
                base.OpenConnection();
            }

            return this._MySqlCommand.ExecuteScalar();
        }

        public DataTable ExecuteDataTable()
        {
            if (base._mySqlConnection.State == ConnectionState.Closed)
            {
                base.OpenConnection();
            }

            var dt = new DataTable();
            using (var reader = this._MySqlCommand.ExecuteReader())
            {
                dt.Load(reader);
            }
            return dt;
        }

        public MySqlDataReader ExecuteDataReader()
        {
            if (base._mySqlConnection.State == ConnectionState.Closed)
            {
                base.OpenConnection();
            }

            return this._MySqlCommand.ExecuteReader();
        }

        public void Dispose()
        {
            this._MySqlCommand.Dispose();
            base.CloseConnection();
            base._mySqlConnection.Dispose();
        }
    }
}
