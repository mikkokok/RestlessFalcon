using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using RestlessFalcon.Helpers;
using RestlessFalcon.Models;

namespace RestlessFalcon.Controllers
{
    /// <summary>
    /// Creates sensor data entries
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class SensorDataController : RestlessFalconControllerBase
    {
        public SensorDataController(IDatabaseHelper dbHelper) : base(dbHelper)
        {
        }
        /// <summary>
        /// Fetches all entries of sensor data. Optional filters sensorid and number of history per days
        /// </summary>
        /// <returns>Returns list of sensor data entries. Optional filters sensorid and number of history per days</returns>
        [HttpGet]
        public async Task<IEnumerable<SensorData>> SensorData(int id = 0, int ago = 0)
        {
            if (id == 0)
            {
                return await GetSensorData();
            }
            if (ago == 0)
            {
                return await GetSensorData(id);
            }
            return await GetSensorData(id, ago);
        }

        private async Task<IEnumerable<SensorData>> GetSensorData()
        {
            using (var conn = _dbHelper.GetDatabaseConnection(Constants.SENSORSCONNECTIONSTRINGNAME))
            {
                const string query = "SELECT * FROM Data";
                conn.Open();
                var results = await conn.QueryAsync<SensorData>(query);
                return results;
            }
        }
        private async Task<IEnumerable<SensorData>> GetSensorData(int id)
        {
            using (var conn = _dbHelper.GetDatabaseConnection(Constants.SENSORSCONNECTIONSTRINGNAME))
            {
                string query = $"SELECT * FROM Data WHERE SensorId = {id}";
                conn.Open();
                var results = await conn.QueryAsync<SensorData>(query);
                return results;
            }
        }

        private async Task<IEnumerable<SensorData>> GetSensorData(int id, int ago)
        {
            using (var conn = _dbHelper.GetDatabaseConnection(Constants.SENSORSCONNECTIONSTRINGNAME))
            {
                string query = $"SELECT * FROM Data WHERE SensorId = {id} AND Time > getdate()-{ago}";
                conn.Open();
                var results = await conn.QueryAsync<SensorData>(query);
                return results;
            }
        }
        /// <summary>
        /// Adds new entry of sensor data
        /// </summary>
        /// <param name="data"></param>
        /// <param name="sensorName"></param>
        /// <param name="authKey"></param>
        /// <returns>HTTP status code</returns>
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] SensorData data, string sensorName, string authKey)
        {
            if (!_authKeyHelper.CheckAuthKeyValidity(authKey))
                return Forbid();
            if (data.Time == null)
            {
                data.Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss").Replace(".", ":");
            }

            var nfi = new CultureInfo("en-US", false).NumberFormat;
            try
            {
                using (var conn = _dbHelper.GetDatabaseConnection(Constants.SENSORSCONNECTIONSTRINGNAME))
                {
                    var query = $"INSERT INTO Data(SensorId, Temperature, Humidity, Pressure, ValvePosition, Time) VALUES ((SELECT id FROM Sensors WHERE Name = '{sensorName}'), {data.Temperature.ToString(nfi)}, {data.Humidity.ToString(nfi)}, {data.Pressure.ToString(nfi)}, {data.ValvePosition}, '{data.Time}')";
                    conn.Open();
                    var results = await conn.QueryAsync<SensorData>(query);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            return Ok();
        }
    }
}
