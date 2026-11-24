using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RestlessFalcon.Helpers;
using RestlessFalcon.Helpers.Impl;
using RestlessFalcon.Models;

namespace RestlessFalcon.Endpoints;

public static class ElectricityPriceEndpoints
{
    public static IEndpointRouteBuilder MapElectricityPriceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/ElectricityPrice", GetPrices).WithTags("ElectricityPrice");
        app.MapPost("/api/ElectricityPrice", AddPrices).WithTags("ElectricityPrice");
        return app;
    }

    private static async Task<IResult> GetPrices(IDatabaseHelper db, [FromQuery] int ago = 0,
        [FromQuery] string date = "")
    {
        var query = new StringBuilder("SELECT * FROM Prices WHERE 1=1");
        var parameters = new DynamicParameters();
        if (!string.IsNullOrWhiteSpace(date))
        {
            if (!DateTime.TryParse(date, out var parsedDate))
                return Results.BadRequest("Invalid date format. Please use ISO 8601.");
            query.Append(" AND Date = @Date");
            parameters.Add("Date", parsedDate);
        }
        else if (ago != 0)
        {
            query.Append(" AND CAST([Date] AS DATE) BETWEEN DATEADD(DAY, -@ago, CAST(GETDATE() AS DATE)) AND CAST(GETDATE() AS DATE)");
            parameters.Add("ago", ago);
        }
        else
            query.Append(" AND CAST([Date] AS DATE) = CAST(GETDATE() AS DATE)");
        try
        {
            using var conn = db.GetDatabaseConnection(Constants.ELECTRICITYPRICECONNECTIONSTRINGNAME);
            var prices = await conn.QueryAsync<ElectricityPrice>(query.ToString(), parameters);
            foreach (var price in prices)
                if (DateTime.TryParseExact(price.Date, "M/d/yyyy HH:mm:ss", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsed))
                    price.Date = parsed.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            return Results.Ok(prices);
        }
        catch (Exception)
        {
            return Results.BadRequest("Error fetching electricity prices.");
        }
    }

    private static async Task<IResult> AddPrices([FromBody] List<ElectricityPrice> prices,
        IDatabaseHelper db, AuthKeyHelper auth, [FromQuery] string? authKey = null)
    {
        if (!AuthorizationHelper.IsAuthorized(auth, authKey)) return Results.StatusCode(403);
        if (prices.Count == 0) return Results.BadRequest("No prices provided.");
        if (!DateTime.TryParseExact(prices[0].Date, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var targetDate))
            return Results.BadRequest($"Invalid date format in first price: {prices[0].Date}");
        try
        {
            using var conn = db.GetDatabaseConnection(Constants.ELECTRICITYPRICECONNECTIONSTRINGNAME);
            conn.Open();
            using var transaction = conn.BeginTransaction();
            var count = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Prices WHERE CAST([Date] AS DATE) = @DateOnly",
                new { DateOnly = targetDate.Date }, transaction);
            if (count is 96 or 92 or 100)
                return Results.Ok($"Already had {count} prices");
            if (count != 0)
                await conn.ExecuteAsync("DELETE FROM Prices WHERE CAST([Date] AS DATE) = @DateOnly",
                    new { DateOnly = targetDate.Date }, transaction);
            await conn.ExecuteAsync("INSERT INTO Prices([Date], Price, Hour) VALUES (@Date, @Price, @Hour)",
                prices, transaction);
            transaction.Commit();
            return Results.Ok($"Inserted {prices.Count} prices");
        }
        catch (Exception)
        {
            return Results.BadRequest("Error inserting prices.");
        }
    }
}
