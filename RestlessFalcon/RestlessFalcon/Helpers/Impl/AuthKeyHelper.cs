using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;
using RestlessFalcon.Models;

namespace RestlessFalcon.Helpers.Impl
{
    public class AuthKeyHelper
    {
        private readonly IDatabaseHelper _dbHelper;
        private readonly IConfiguration _config;

        public AuthKeyHelper(IDatabaseHelper dbHelper, IConfiguration config)
        {
            _dbHelper = dbHelper;
            _config = config;

        }

        public bool CheckAuthKeyValidity(string authKey)
        {
            try
            {
                if (authKey == _config.GetSection("MasterKey").Value)
                {
                    return true;
                }

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
            }
            catch
            {
                // ignored
            }

            return false;
        }
        public async Task<string> RenewAuthKey(int id)
        {
            using (var conn = _dbHelper.GetDatabaseConnection(Constants.AUTHORISATIONCONNECTIONSTRINGNAME))
            {
                var query = $"DELETE FROM AuthKey WHERE id = {id}";
                conn.Open();
                await conn.QueryAsync<AuthKey>(query);
                var newKey = Guid.NewGuid();
                query = $"INSERT INTO AuthKey(Value) VALUES ('{newKey.ToString()}')";
                await conn.QueryAsync(query);
                return newKey.ToString();
            }
        }

    }
}
