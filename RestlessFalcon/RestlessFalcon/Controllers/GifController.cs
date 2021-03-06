﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Mvc;
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
