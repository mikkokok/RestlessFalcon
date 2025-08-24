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
    public class AuthorisationController(IDatabaseHelper dbHelper) : RestlessFalconControllerBase(dbHelper)
    {
        [HttpGet]
        public async Task<ActionResult<string>> GetUsers()
        {
            using IDbConnection conn = _dbHelper.GetDatabaseConnection(Constants.AUTHORISATIONCONNECTIONSTRINGNAME);
            var query = "SELECT * FROM AuthUser";
            conn.Open();
            var result = await conn.QueryAsync<AuthUser>(query);
            return Ok(result.Select(user => user.Name ?? string.Empty));
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] string authKey, int keyId)
        {
            if (!_authKeyHelper.CheckAuthKeyValidity(authKey))
                return Forbid();
            await _authKeyHelper.RenewAuthKey(keyId);

            return Ok();
        }
    }
}
