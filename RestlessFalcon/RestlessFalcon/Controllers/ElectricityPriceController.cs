using Dapper;
using Microsoft.AspNetCore.Mvc;
using RestlessFalcon.Helpers;
using RestlessFalcon.Models;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System;
using System.Data.SqlClient;
using System.Linq;

namespace RestlessFalcon.Controllers
{

    /// <summary>
    /// Creates entries for electricity price
    /// </summary>
    [Route("api/[controller]")]
    public class ElectricityPriceController : RestlessFalconControllerBase
    {
        public ElectricityPriceController(IDatabaseHelper dbHelper) : base(dbHelper)
        {
        }
        /// <summary>
        /// Fetches all entries of electricity price data. Optional filters number of history per days and entries for exact date
        /// </summary>
        /// <returns>Returns list of sensor data entries. Optional filters number of history per days and entries for exact date</returns>
        [HttpGet]
        public async Task<IEnumerable<ElectricityPrice>> ElectricityPrice(int ago = 0, string date = "")
        {
            return await GetElectricityPrice(ago, date);
        }

        private async Task<IEnumerable<ElectricityPrice>> GetElectricityPrice(int ago, string date)
        {
            string query = "SELECT * FROM Prices WHERE 1=1";
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
        /// Adds new entry of electricity prices
        /// </summary>
        /// <param name="prices"></param>
        /// <param name="authKey"></param>
        /// <returns>HTTP status code</returns>
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] List<ElectricityPrice> prices, string authKey)
        {
            if (!_authKeyHelper.CheckAuthKeyValidity(authKey))
                return Forbid();
            _ = new CultureInfo("en-US", false).NumberFormat;
            var query = $"SELECT * FROM Prices WHERE CAST(Date AS DATE) = '{prices[0].Date}'";

            try
            {
                using (var conn = _dbHelper.GetDatabaseConnection(Constants.ELECTRICITYPRICECONNECTIONSTRINGNAME))
                {
                    conn.Open();
                    var results = await conn.QueryAsync<ElectricityPrice>(query);
                    if (results.Any() && results.Count() < 24)
                    {
                        await conn.ExecuteAsync($"DELETE FROM Prices WHERE CAST(Date AS DATE) = {prices[0].Date}");
                    }
                    else if (results.Count() == 24)
                    {
                        return Ok();
                    }
                }

            }
            catch (Exception ex)
            {
                _logger.WriteErrorLog($"Exception {ex.Message} used query {query}");
                throw;
            }
            
            query = "INSERT INTO Prices(Date, Price, Hour) VALUES (@Date, @Price, @Hour)";
            try
            {
                using (var conn = _dbHelper.GetDatabaseConnection(Constants.ELECTRICITYPRICECONNECTIONSTRINGNAME))
                {
                    conn.Open();
                    
                    await conn.ExecuteAsync(query, prices);
                    
                }
            }
            catch (Exception ex)
            {
                _logger.WriteErrorLog($"Exception {ex.Message} used query {query}");
            }
            return Ok();
        }
    }
}
