using System;
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

public static class SensorEndpoints
{
    public static IEndpointRouteBuilder MapSensorEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/Sensor", GetSensors).WithTags("Sensor");
        app.MapPost("/api/Sensor", AddSensor).WithTags("Sensor");
        app.MapDelete("/api/Sensor/{id:int}", DeleteSensor).WithTags("Sensor");
        return app;
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
        if (!AuthorizationHelper.IsAuthorized(auth, authKey)) return Results.StatusCode(403);
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
        if (!AuthorizationHelper.IsAuthorized(auth, authKey)) return Results.StatusCode(403);
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
}
