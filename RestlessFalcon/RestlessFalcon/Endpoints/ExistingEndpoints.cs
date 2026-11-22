using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

public static class ExistingEndpoints
{
    public static IEndpointRouteBuilder MapExistingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/Authorisation", GetUsers).WithTags("Authorisation");
        app.MapPost("/api/Authorisation", RenewKey).WithTags("Authorisation");
        app.MapGet("/api/Charging", GetCharges).WithTags("Charging");
        app.MapPost("/api/Charging", AddCharge).WithTags("Charging");
        app.MapGet("/api/ElectricityPrice", GetPrices).WithTags("ElectricityPrice");
        app.MapPost("/api/ElectricityPrice", AddPrices).WithTags("ElectricityPrice");
        app.MapGet("/api/Sensor", GetSensors).WithTags("Sensor");
        app.MapPost("/api/Sensor", AddSensor).WithTags("Sensor");
        app.MapDelete("/api/Sensor/{id:int}", DeleteSensor).WithTags("Sensor");
        app.MapGet("/api/SensorData", GetSensorData).WithTags("SensorData");
        app.MapPost("/api/SensorData", AddSensorData).WithTags("SensorData");
        return app;
    }

    private static bool IsAuthorized(AuthKeyHelper auth, string? key) =>
        !string.IsNullOrWhiteSpace(key) && auth.CheckAuthKeyValidity(key);

    private static async Task<IResult> GetUsers(IDatabaseHelper db)
    {
        using var conn = db.GetDatabaseConnection(Constants.AUTHORISATIONCONNECTIONSTRINGNAME);
        var users = await conn.QueryAsync<AuthUser>("SELECT * FROM AuthUser");
        return Results.Ok(users.Select(user => user.Name ?? string.Empty));
    }

    private static async Task<IResult> RenewKey([FromBody] string authKey, [FromQuery] int keyId,
        AuthKeyHelper auth)
    {
        if (!IsAuthorized(auth, authKey)) return Results.StatusCode(403);
        await auth.RenewAuthKey(keyId);
        return Results.Ok();
    }

    private static async Task<IResult> GetCharges(IDatabaseHelper db, [FromQuery] int ago = 0,
        [FromQuery] string date = "")
    {
        var query = new StringBuilder("SELECT * FROM Charges WHERE 1=1");
        if (ago != 0)
            query.Append(" AND CAST([Date] AS DATE) BETWEEN DATEADD(DAY, -@ago, CAST(GETDATE() AS DATE)) AND CAST(GETDATE() AS DATE)");
        if (!string.IsNullOrWhiteSpace(date))
            query.Append(" AND CAST([Date] AS DATE) = @date");
        else if (ago == 0)
            query.Append(" AND CAST([Date] AS DATE) = CAST(GETDATE() AS DATE)");
        try
        {
            using var conn = db.GetDatabaseConnection(Constants.ELECTRICITYPRICECONNECTIONSTRINGNAME);
            return Results.Ok(await conn.QueryAsync<CarCharge>(query.ToString(), new { ago, date }));
        }
        catch (Exception)
        {
            return Results.BadRequest("Error fetching car charge amounts.");
        }
    }

    private static async Task<IResult> AddCharge([FromBody] CarCharge charge, IDatabaseHelper db,
        AuthKeyHelper auth, [FromQuery] string? authKey = null)
    {
        if (!IsAuthorized(auth, authKey)) return Results.StatusCode(403);
        try
        {
            using var conn = db.GetDatabaseConnection(Constants.ELECTRICITYPRICECONNECTIONSTRINGNAME);
            await conn.ExecuteAsync("INSERT INTO Charges([Date], Hour, Charged) VALUES (@Date, @Hour, @Charged)", charge);
            return Results.Ok();
        }
        catch (Exception)
        {
            return Results.BadRequest("Error adding car charge entry.");
        }
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
        if (!IsAuthorized(auth, authKey)) return Results.StatusCode(403);
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

    private static async Task<IResult> GetSensors(IDatabaseHelper db)
    {
        try
        {
            using var conn = db.GetDatabaseConnection(Constants.SENSORSCONNECTIONSTRINGNAME);
            return Results.Ok(await conn.QueryAsync<Sensor>("SELECT * FROM Sensors"));
        }
        catch (Exception)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> AddSensor([FromBody] Sensor sensor, IDatabaseHelper db,
        AuthKeyHelper auth, [FromQuery] string? authKey = null)
    {
        if (!IsAuthorized(auth, authKey)) return Results.StatusCode(403);
        try
        {
            using var conn = db.GetDatabaseConnection(Constants.SENSORSCONNECTIONSTRINGNAME);
            await conn.ExecuteAsync("INSERT INTO Sensors(Name, Location, Model) VALUES (@Name, @Location, @Model)", sensor);
            return Results.Ok();
        }
        catch (Exception)
        {
            return Results.BadRequest("Error inserting sensor.");
        }
    }

    private static async Task<IResult> DeleteSensor(int id, IDatabaseHelper db,
        AuthKeyHelper auth, [FromQuery] string? authKey = null)
    {
        if (!IsAuthorized(auth, authKey)) return Results.StatusCode(403);
        try
        {
            using var conn = db.GetDatabaseConnection(Constants.SENSORSCONNECTIONSTRINGNAME);
            await conn.ExecuteAsync("DELETE FROM Sensors WHERE Id = @id", new { id });
            return Results.Ok();
        }
        catch (Exception)
        {
            return Results.BadRequest("Error deleting sensor.");
        }
    }

    private static async Task<IResult> GetSensorData(IDatabaseHelper db, [FromQuery] int id = 0,
        [FromQuery] int ago = 0, [FromQuery] int amount = 0)
    {
        if (id < 0 || ago < 0 || amount < 0)
            return Results.BadRequest("Filters must be non-negative.");
        var query = new StringBuilder(amount == 0 ? "SELECT * FROM Data WHERE 1=1" : "SELECT TOP (@amount) * FROM Data WHERE 1=1");
        if (id != 0) query.Append(" AND SensorId = @id");
        if (ago != 0) query.Append(" AND Time > DATEADD(DAY, -@ago, GETDATE())");
        if (id != 0 || ago != 0 || amount != 0) query.Append(" ORDER BY Time DESC");
        using var conn = db.GetDatabaseConnection(Constants.SENSORSCONNECTIONSTRINGNAME);
        return Results.Ok(await conn.QueryAsync<SensorData>(query.ToString(), new { id, ago, amount }));
    }

    private static async Task<IResult> AddSensorData([FromBody] SensorData data, [FromQuery] string sensorName,
        IDatabaseHelper db, AuthKeyHelper auth, [FromQuery] string? authKey = null)
    {
        if (!IsAuthorized(auth, authKey)) return Results.StatusCode(403);
        data.Time ??= DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        try
        {
            using var conn = db.GetDatabaseConnection(Constants.SENSORSCONNECTIONSTRINGNAME);
            await conn.ExecuteAsync("""
                INSERT INTO Data(SensorId, Temperature, Humidity, Pressure, ValvePosition, UsedPower, Time, BurnerUseTime, PowerYield)
                VALUES ((SELECT Id FROM Sensors WHERE Name = @sensorName), @Temperature, @Humidity, @Pressure,
                    @ValvePosition, @UsedPower, @Time, @BurnerUseTime, @PowerYield)
                """, new { sensorName, data.Temperature, data.Humidity, data.Pressure, data.ValvePosition,
                    data.UsedPower, data.Time, data.BurnerUseTime, data.PowerYield });
            return Results.Ok();
        }
        catch (Exception)
        {
            return Results.BadRequest("Error inserting data.");
        }
    }
}
