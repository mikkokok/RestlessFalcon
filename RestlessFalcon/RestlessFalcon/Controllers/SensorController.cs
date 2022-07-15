using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using RestlessFalcon.Helpers;
using RestlessFalcon.Models;

namespace RestlessFalcon.Controllers
{
    /// <summary>
    /// API Controller for handling new sensors in the environment
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class SensorController : RestlessFalconControllerBase
    {
        public SensorController(IDatabaseHelper dbHelper) : base(dbHelper)
        {
        }
        /// <summary>
        /// Fetches all sensors from the database
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<IEnumerable<Sensor>> GetSensors()
        {
            try
            {
                using var conn = _dbHelper.GetDatabaseConnection(Constants.SENSORSCONNECTIONSTRINGNAME);
                const string query = "SELECT * FROM Sensors";
                conn.Open();
                var results = await conn.QueryAsync<Sensor>(query);
                return results;
            }
            catch (Exception ex)
            {
                _logger.WriteErrorLog(ex.Message);
                throw;
            }

        }
        /// <summary>
        /// Creates new sensor for the environment
        /// </summary>
        /// <param name="sensor"></param>
        /// <param name="authKey"></param>
        /// <returns>HTTP status code</returns>
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
            catch (Exception ex)
            {
                _logger.WriteErrorLog(ex.Message);
                return NotFound();
            }
            return Ok();
        }
        /// <summary>
        /// Deletes sensor
        /// </summary>
        /// <param name="id"></param>
        /// <param name="authKey"></param>
        /// <returns>HTTP status code</returns>
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
            catch (Exception ex)
            {
                _logger.WriteErrorLog(ex.Message);
                return NotFound();
            }
            return Ok();
        }
    }
}
