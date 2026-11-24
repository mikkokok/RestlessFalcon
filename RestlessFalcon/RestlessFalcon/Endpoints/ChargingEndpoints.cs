using System;
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

public static class ChargingEndpoints
{
    public static IEndpointRouteBuilder MapChargingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/Charging", GetCharges).WithTags("Charging");
        app.MapPost("/api/Charging", AddCharge).WithTags("Charging");
        return app;
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
        if (!AuthorizationHelper.IsAuthorized(auth, authKey)) return Results.StatusCode(403);
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
}
