using System;
using System.Collections.Generic;
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
        private readonly Logger _logger;
        private List<AuthKey> _authKeys = [];
        private DateTime _lastCleaned = DateTime.MinValue;

        public AuthKeyHelper(IDatabaseHelper dbHelper, IConfiguration config)
        {
            _dbHelper = dbHelper;
            _config = config;
            _logger = new Logger();
            _authKeys.Add(new AuthKey { Value = _config.GetSection("MasterKey").Value, Id = 1 });
            _lastCleaned = DateTime.Now;
        }

        public bool CheckAuthKeyValidity(string authKey)
        {
            try
            {
                if (_authKeys.Select(entry => entry.Value).Contains(authKey))
                {
                    return true;
                }

                using var conn = _dbHelper.GetDatabaseConnection(Constants.AUTHORISATIONCONNECTIONSTRINGNAME);
                var query = "SELECT * FROM AuthKey";
                conn.Open();
                var result = conn.Query<AuthKey>(query);
                var keyEntry = result.FirstOrDefault(entry => entry.Value == authKey);
                if (keyEntry != null)
                {
                    _authKeys.Add(new AuthKey { Value = authKey, Id = keyEntry.Id});
                    return true;
                }
                CleanAuthKeys();
            }
            catch(Exception ex) 
            {
                _logger.WriteErrorLog($"Got error in AuthKeyHelper, {ex}");
            }

            return false;
        }
        public async Task<string> RenewAuthKey(int id)
        {
            using var conn = _dbHelper.GetDatabaseConnection(Constants.AUTHORISATIONCONNECTIONSTRINGNAME);
            var query = $"DELETE FROM AuthKey WHERE id = {id}";
            conn.Open();
            await conn.QueryAsync<AuthKey>(query);
            var newKey = Guid.NewGuid();
            query = $"INSERT INTO AuthKey(Value) VALUES ('{newKey.ToString()}')";
            await conn.QueryAsync(query);
            return newKey.ToString();
        }

        private void CleanAuthKeys()
        {
            if (_lastCleaned.Date == DateTime.Now.Date)
                return;
            _authKeys.RemoveAll(a => a.Id != 1);
            _lastCleaned = DateTime.Now;
        }
    }
}
