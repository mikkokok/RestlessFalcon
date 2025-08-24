using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace RestlessFalcon.Helpers.Impl
{
    public class DatabaseHelper : IDatabaseHelper
    {
        public DatabaseHelper(IConfiguration config)
        {
            Config = config;
        }

        public IDbConnection GetDatabaseConnection(string connectionString)
        {
            
            return new SqlConnection(Config.GetConnectionString(connectionString));
        }

        public IConfiguration Config { get; }
    }
}
