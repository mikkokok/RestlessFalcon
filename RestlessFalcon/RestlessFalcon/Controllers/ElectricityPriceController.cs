using Dapper;
using Microsoft.AspNetCore.Mvc;
using RestlessFalcon.Helpers;
using RestlessFalcon.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RestlessFalcon.Controllers
{

    /// <summary>
    /// Creates entries for electricity price
    /// </summary>
    [Route("api/[controller]")]
    public class ElectricityPriceController(IDatabaseHelper dbHelper) : RestlessFalconControllerBase(dbHelper)
    {

        /// <summary>
        /// Fetches all entries of electricity price data. Optional filters number of history per days and entries for exact date
        /// </summary>
        /// <returns>Returns list of sensor data entries. Optional filters number of history per days and entries for exact date</returns>
        [HttpGet]
        public async Task<ActionResult<ElectricityPrice>> GetElectricityPrice(int ago, string date = "")
        {
            var queryBuilder = new StringBuilder("SELECT * FROM Prices WHERE 1=1");
            var parameters = new DynamicParameters();

            if (!string.IsNullOrWhiteSpace(date))
            {
                if (DateTime.TryParse(date, out DateTime parsedDate))
                {
                    queryBuilder.Append(" AND Date = @Date");
                    parameters.Add("@Date", parsedDate);
                }
                else
                {
                    return BadRequest("Invalid date format. Please use ISO 8601.");
                }
            }
            else if (ago != 0)
            {
                queryBuilder.Append(" AND CAST(Date AS DATE) BETWEEN DATEADD(DAY, -@ago, CAST(GETDATE() AS DATE)) AND CAST(GETDATE() AS DATE)");
                parameters.Add("@ago", ago);
            }
            else
            {
                queryBuilder.Append(" AND CAST(Date AS DATE) = CAST(GETDATE() AS DATE)");
            }

            try
            {
                using var conn = _dbHelper.GetDatabaseConnection(Constants.ELECTRICITYPRICECONNECTIONSTRINGNAME);
                conn.Open();
                var results = await conn.QueryAsync<ElectricityPrice>(queryBuilder.ToString(), parameters);

                foreach (var result in results)
                {
                    if (!string.IsNullOrEmpty(result.Date))
                    {
                        if (DateTime.TryParseExact(result.Date, "M/d/yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateValue))
                        {
                            result.Date = dateValue.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                        }
                        else
                        {
                            _logger.WriteWarningLog($"Failed to parse date: '{result.Date}'");
                        }
                    }
                }
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.WriteErrorLog(ex.Message);
                return BadRequest($"Error fetching electricity prices: {ex.Message}");
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

            if (prices == null || prices.Count == 0)
                return BadRequest("No prices provided.");

            if (!DateTime.TryParseExact(
                    prices[0].Date,
                    "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var targetDate))
            {
                return BadRequest($"Invalid date format in first price: {prices[0].Date}");
            }

            try
            {
                using var conn = _dbHelper.GetDatabaseConnection(Constants.ELECTRICITYPRICECONNECTIONSTRINGNAME);
                conn.Open();

                var existing = await conn.QueryAsync<ElectricityPrice>(
                    "SELECT * FROM Prices WHERE CAST([Date] AS DATE) = @DateOnly",
                    new { DateOnly = targetDate.Date });

                var count = existing.Count();

                if (existing.Any() && count != 96 && count != 92 && count != 100)
                {
                    await conn.ExecuteAsync(
                        "DELETE FROM Prices WHERE CAST([Date] AS DATE) = @DateOnly",
                        new { DateOnly = targetDate.Date });
                }
                else if (count == 96 || count == 92 || count == 100)
                {
                    return Ok($"Already had {count} prices");
                }

                const string insertSql = "INSERT INTO Prices([Date], Price, Hour) VALUES (@Date, @Price, @Hour)";
                await conn.ExecuteAsync(insertSql, prices);
            }
            catch (Exception ex)
            {
                _logger.WriteErrorLog($"Exception {ex.Message}");
                return BadRequest($"Error inserting prices: {ex.Message}");
            }

            return Ok($"Inserted {prices.Count} prices");
        }
    }
}
