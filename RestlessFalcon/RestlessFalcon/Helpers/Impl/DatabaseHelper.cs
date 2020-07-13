using System.Data;
using System.Data.SqlClient;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Configuration;

namespace RestlessFalcon.Helpers.Impl
{
    public class DatabaseHelper : IDatabaseHelper
    {
        private readonly IConfiguration _config;
       

        public DatabaseHelper(IConfiguration config)
        {
            _config = config;
        }

        

        public IDbConnection GetDatabaseConnection(string connectionString)
        {
            return new SqlConnection(_config.GetConnectionString(connectionString));
        }

        public IConfiguration Config => _config;
    }
}
