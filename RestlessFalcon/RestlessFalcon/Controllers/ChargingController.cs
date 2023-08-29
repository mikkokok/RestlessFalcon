using Dapper;
using Microsoft.AspNetCore.Mvc;
using RestlessFalcon.Helpers;
using RestlessFalcon.Models;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace RestlessFalcon.Controllers
{

    /// <summary>
    /// Creates and fetches entries for car charging
    /// </summary>
    [Route("api/[controller]")]
    public class ChargingController : RestlessFalconControllerBase
    {
        public ChargingController(IDatabaseHelper dbHelper) : base(dbHelper)
        {
        }
        /// <summary>
        /// Fetches all entries of car charge data. Optional filters number of history per days and entries for exact date
        /// </summary>
        /// <returns>Returns list of car charge entries. Optional filters number of history per days and entries for exact date</returns>
        [HttpGet]
        public async Task<IEnumerable<ElectricityPrice>> ChargeAmount(int ago = 0, string date = "")
        {
            return await GetChargeAmount(ago, date);
        }

        private async Task<IEnumerable<ElectricityPrice>> GetChargeAmount(int ago, string date)
        {
            string query = "SELECT * FROM Charges WHERE 1=1";
            string agoQuery = $" AND CAST(date AS DATE) BETWEEN DATEADD(DAY, -{ago}, CAST(GETDATE() AS DATE)) AND CAST(GETDATE() AS DATE)";
            string dateQuery = $" AND CAST(Date AS DATE) = '{date}'";

            if (ago != 0)
            {
                query += agoQuery;
            }
            if (!string.IsNullOrWhiteSpace(date))
            {
                query += dateQuery;
            }
            else if (ago == 0)
            {
                query += " AND CAST(Date AS DATE) = CAST(GETDATE() AS DATE)";
            }

            try
            {
                using var conn = _dbHelper.GetDatabaseConnection(Constants.ELECTRICITYPRICECONNECTIONSTRINGNAME);
                conn.Open();
                var results = await conn.QueryAsync<ElectricityPrice>(query);
                return results;
            }
            catch (Exception ex)
            {
                _logger.WriteErrorLog(ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Adds new entry of car charge
        /// </summary>
        /// <param name="charge"></param>
        /// <param name="authKey"></param>
        /// <returns>HTTP status code</returns>
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CarCharge charge, string authKey)
        {
            if (!_authKeyHelper.CheckAuthKeyValidity(authKey))
                return Forbid();

            _ = new CultureInfo("en-US", false).NumberFormat;
            var query = $"INSERT INTO Charges(Date, Hour, Charged) VALUES ('{charge.Date}', '{charge.Hour}', '{charge.Charged}' )";
            try
            {
                using var conn = _dbHelper.GetDatabaseConnection(Constants.ELECTRICITYPRICECONNECTIONSTRINGNAME);
                conn.Open();
                var results = await conn.QueryAsync<CarCharge>(query);
            }
            catch (Exception ex)
            {
                _logger.WriteErrorLog($"Exception {ex.Message} used query {query}");
            }
            return Ok();
        }
    }
}
