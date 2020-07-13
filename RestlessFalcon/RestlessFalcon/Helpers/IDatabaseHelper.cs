using System.Data;
using Microsoft.Extensions.Configuration;

namespace RestlessFalcon.Helpers
{
    public interface IDatabaseHelper
    {
        IDbConnection GetDatabaseConnection(string connectionString);
        IConfiguration Config { get; }
    }
}