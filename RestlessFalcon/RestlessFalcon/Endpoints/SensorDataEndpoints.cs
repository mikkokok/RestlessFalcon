using System;
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

public static class SensorDataEndpoints
{
    public static IEndpointRouteBuilder MapSensorDataEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/SensorData", GetSensorData).WithTags("SensorData");
        app.MapPost("/api/SensorData", AddSensorData).WithTags("SensorData");
        return app;
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
        if (!AuthorizationHelper.IsAuthorized(auth, authKey)) return Results.StatusCode(403);
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
