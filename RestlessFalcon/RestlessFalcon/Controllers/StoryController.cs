using System.Collections.Generic;
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
    public class StoryController : RestlessFalconControllerBase
    {
        // GET: api/Story
        public StoryController(IDatabaseHelper dbHelper) : base(dbHelper)
        {
        }

        [HttpGet]
        public async Task<List<string>> Get()
        {
            using (var conn = _dbHelper.GetDatabaseConnection(Constants.STORYCONNECTIONSTRINGNAME))
            {
                var query = "SELECT * FROM Story";
                conn.Open();
                var results = await conn.QueryAsync<Story>(query);
                return results.Select(result => result.Tale).ToList();
            }
        }

        // GET: api/Story/5
        [HttpGet("{id}", Name = "Get")]
        public async Task<string> Get(int id)
        {
            using (var conn = _dbHelper.GetDatabaseConnection(Constants.STORYCONNECTIONSTRINGNAME))
            {
                var query = $"SELECT * FROM Story WHERE id = {id}";
                conn.Open();
                var result = await conn.QueryAsync<Story>(query);
                return result.First().Tale;
            }
        }

        // POST: api/Story
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] string value, string authKey)
        {
            if (!_authKeyHelper.CheckAuthKeyValidity(authKey))
                return Forbid();
            using (var conn = _dbHelper.GetDatabaseConnection(Constants.STORYCONNECTIONSTRINGNAME))
            {
                var query = $"INSERT INTO Story(Tale) VALUES ('{value}')";
                conn.Open();
                await conn.QueryAsync<Story>(query);
            }
            return Ok();
        }
    }
}
