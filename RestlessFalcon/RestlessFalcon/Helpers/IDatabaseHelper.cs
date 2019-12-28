using System.Data;

namespace RestlessFalcon.Helpers
{
    public interface IDatabaseHelper
    {
        IDbConnection GetDatabaseConnection(string connectionString);
    }
}