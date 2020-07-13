using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using RestlessFalcon.Helpers;
using RestlessFalcon.Models;

namespace RestlessFalcon.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SensorController : RestlessFalconControllerBase
    {
        public SensorController(IDatabaseHelper dbHelper) : base(dbHelper)
        {
        }

        [HttpGet]
        public async Task<IEnumerable<Sensor>> GetSensors()
        {
            using (var conn = _dbHelper.GetDatabaseConnection(Constants.SENSORSCONNECTIONSTRINGNAME))
            {
                const string query = "SELECT * FROM Sensors";
                conn.Open();
                var results = await conn.QueryAsync<Sensor>(query);
                return results;
            }
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Sensor sensor, string authKey)
        {
            if (!_authKeyHelper.CheckAuthKeyValidity(authKey))
                return Forbid();

            try
            {
                using (var conn = _dbHelper.GetDatabaseConnection(Constants.SENSORSCONNECTIONSTRINGNAME))
                {
                    var query = $"INSERT INTO Sensors(Name, Location, Model) VALUES ('{sensor.Name}', '{sensor.Location}', '{sensor.Model}')";
                    conn.Open();
                    await conn.QueryAsync<Sensor>(query);
                }
            }
            catch
            {
                return NotFound();
            }
            return Ok();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id, string authKey)
        {
            if (!_authKeyHelper.CheckAuthKeyValidity(authKey))
                return Forbid();
            try
            {
                using (var conn = _dbHelper.GetDatabaseConnection(Constants.SENSORSCONNECTIONSTRINGNAME))
                {
                    var query = $"DELETE FROM Sensors WHERE Id = {id}";
                    conn.Open();
                    await conn.QueryAsync<Sensor>(query);
                }
            }
            catch
            {
                return NotFound();
            }
            return Ok();
        }
    }
}
