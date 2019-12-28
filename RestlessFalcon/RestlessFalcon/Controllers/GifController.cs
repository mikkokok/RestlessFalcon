using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Update;
using RestlessFalcon.Helpers;
using RestlessFalcon.Models;

namespace RestlessFalcon.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GifController : RestlessFalconControllerBase
    {
        public GifController(IDatabaseHelper dbHelper) : base(dbHelper)
        {

        }
        // GET: api/Gif
        [HttpGet]
        public async Task<IEnumerable<GifUrl>> Get()
        {
            var ret = new Dictionary<string, string>();
            using (var conn = _dbHelper.GetDatabaseConnection(Constants.GIFREPOCONNECTIONSTRINGNAME))
            {
                const string query = "SELECT * FROM GifUrl";
                conn.Open();
                var results = await conn.QueryAsync<GifUrl>(query);
                return results;
            }
        }


        // POST: api/Gif
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] GifUrl data)
        {
            using (var conn = _dbHelper.GetDatabaseConnection(Constants.GIFREPOCONNECTIONSTRINGNAME))
            {
                var query = $"INSERT INTO GifUrl(URL) VALUES ('{data.Url}', '{data.Comment}')";
                conn.Open();
                await conn.QueryAsync<GifUrl>(query);
            }
            return Ok();
        }

    }
}
