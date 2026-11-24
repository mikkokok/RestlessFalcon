using System.Linq;
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

public static class AuthorisationEndpoints
{
    public static IEndpointRouteBuilder MapAuthorisationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/Authorisation", GetUsers).WithTags("Authorisation");
        app.MapPost("/api/Authorisation", RenewKey).WithTags("Authorisation");
        return app;
    }

    private static async Task<IResult> GetUsers(IDatabaseHelper db)
    {
        using var conn = db.GetDatabaseConnection(Constants.AUTHORISATIONCONNECTIONSTRINGNAME);
        var users = await conn.QueryAsync<AuthUser>("SELECT * FROM AuthUser");
        return Results.Ok(users.Select(user => user.Name ?? string.Empty));
    }

    private static async Task<IResult> RenewKey([FromBody] string authKey, [FromQuery] int keyId,
        AuthKeyHelper auth)
    {
        if (!AuthorizationHelper.IsAuthorized(auth, authKey)) return Results.StatusCode(403);
        await auth.RenewAuthKey(keyId);
        return Results.Ok();
    }
}
