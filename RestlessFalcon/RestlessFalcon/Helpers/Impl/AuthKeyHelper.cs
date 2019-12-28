using System;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Dapper;
using RestlessFalcon.Models;

namespace RestlessFalcon.Helpers.Impl
{
    public class AuthKeyHelper
    {
        private IDatabaseHelper _dbHelper;

        public AuthKeyHelper(IDatabaseHelper dbHelper)
        {
            _dbHelper = dbHelper;
        }

        public bool CheckAuthKeyValidity(string authKey)
        {
            using (var conn = _dbHelper.GetDatabaseConnection(Constants.AUTHORISATIONCONNECTIONSTRINGNAME))
            {
                var query = "SELECT * FROM AuthKey";
                conn.Open();
                var result = conn.Query<AuthKey>(query);
                if (result.Select(key => key.Value).Contains(authKey))
                {
                    return true;
                }
            }
            return false;
        }
        public async Task RenewAuthKey()
        {
            using (var conn = _dbHelper.GetDatabaseConnection(Constants.AUTHORISATIONCONNECTIONSTRINGNAME))
            {
                var query = "DELETE FROM AuthKey";
                conn.Open();
                await conn.QueryAsync<AuthKey>(query);
                var newKey = Guid.NewGuid();
                query = $"INSERT INTO AuthKey(Value) VALUES ('{newKey}')";
                await conn.QueryAsync(query);
            }
        }

    }
}
