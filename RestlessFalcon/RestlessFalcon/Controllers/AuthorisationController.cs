using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using RestlessFalcon.Helpers;
using RestlessFalcon.Models;

namespace RestlessFalcon.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthorisationController : RestlessFalconControllerBase
    {
        private string ConnectionStringName = Constants.AUTHORISATIONCONNECTIONSTRINGNAME;
        
        public AuthorisationController(IDatabaseHelper dbHelper) : base(dbHelper)
        {
        }

        [HttpGet]
        public IEnumerable<string> GetUsers(string authKey)
        {
            using (IDbConnection conn = _dbHelper.GetDatabaseConnection(ConnectionStringName))
            {
                var query = "SELECT * FROM AuthUser";
                conn.Open();
                var result = conn.Query<AuthUser>(query);
                return result.Select(user => user.Name);
            }
        }

        [HttpPost("authkey")]
        public async Task<IActionResult> AuthKey(int keyId, string authKey)
        {
            if (!_authKeyHelper.CheckAuthKeyValidity(authKey))
                return Forbid();
            await _authKeyHelper.RenewAuthKey(keyId);

            return Ok();
        }
    }
}
