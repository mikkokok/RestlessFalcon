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
    public class SensorDataController : RestlessFalconControllerBase
    {
        public SensorDataController(IDatabaseHelper dbHelper) : base(dbHelper)
        {
        }

        [HttpGet]
        public async Task<IEnumerable<SensorData>> GetSensorData()
        {
            using (var conn = _dbHelper.GetDatabaseConnection(Constants.SENSORSCONNECTIONSTRINGNAME))
            {
                const string query = "SELECT * FROM Data";
                conn.Open();
                var results = await conn.QueryAsync<SensorData>(query);
                return results;
            }
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] SensorData data, string authKey)
        {
        }
        
    }
}
