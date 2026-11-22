using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RestlessFalcon.Helpers.Impl;
using RestlessFalcon.State;

namespace RestlessFalcon.Endpoints;

public static class StateEndpoints
{
    public const int MaxValueBytes = 1024 * 1024;
    private static readonly Regex Identifier = new("\\A[A-Za-z0-9][A-Za-z0-9_.-]*\\z", RegexOptions.CultureInvariant);

    public static IEndpointRouteBuilder MapStateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/state/{namespace}").WithTags("State");
        group.AddEndpointFilter(async (context, next) =>
        {
            var headers = context.HttpContext.Request.Headers["X-Auth-Key"];
            if (headers.Count != 1 || string.IsNullOrWhiteSpace(headers[0]) ||
                !context.HttpContext.RequestServices.GetRequiredService<AuthKeyHelper>().CheckAuthKeyValidity(headers[0]!))
                return Results.StatusCode(403);
            var ns = context.HttpContext.Request.RouteValues["namespace"]?.ToString();
            if (!IsValidIdentifier(ns, 100))
                return Results.BadRequest("Invalid namespace (1-100 ASCII letters/digits, dots, underscores or hyphens; start with a letter/digit).");
            if (context.HttpContext.Request.RouteValues.TryGetValue("key", out var key) &&
                !IsValidIdentifier(key?.ToString(), 200))
                return Results.BadRequest("Invalid key (1-200 ASCII letters/digits, dots, underscores or hyphens; start with a letter/digit).");
            try
            {
                return await next(context);
            }
            catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Never log request headers, payloads, SQL exception messages or connection strings.
                context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("StateEndpoints").LogError("State operation failed ({ExceptionType}).", ex.GetType().Name);
                return Results.Problem("The state operation could not be completed.", statusCode: 500);
            }
        });
        group.MapGet("", List).WithName("ListStateKeys").Produces<IReadOnlyList<StateKey>>().Produces(400).Produces(403);
        group.MapGet("/{key}", Get).WithName("GetState").Produces<StateEntry>().Produces(404).Produces(403);
        group.MapPost("", Create).WithName("CreateState").Produces<StateEntry>(201).Produces(409).Produces(400).Produces(403);
        group.MapPut("/{key}", Update).WithName("UpdateState").Produces<StateEntry>().Produces(404).Produces(400).Produces(403);
        group.MapDelete("/{key}", Delete).WithName("DeleteState").Produces(204).Produces(404).Produces(403);
        return app;
    }

    public static bool IsValidIdentifier(string? value, int maxLength) =>
        !string.IsNullOrEmpty(value) && value.Length <= maxLength && Identifier.IsMatch(value);

    public static bool IsValidValue(JsonElement value) => value.ValueKind != JsonValueKind.Undefined &&
        Encoding.UTF8.GetByteCount(value.GetRawText()) <= MaxValueBytes;

    private static async Task<IResult> List(string @namespace, IStateStore store, CancellationToken cancellationToken,
        [FromQuery] int skip = 0, [FromQuery] int take = 100)
    {
        if (skip < 0 || take is < 1 or > 1000)
            return Results.BadRequest("skip must be non-negative; take must be between 1 and 1000.");
        return Results.Ok(await store.ListAsync(@namespace, skip, take, cancellationToken));
    }

    private static async Task<IResult> Get(string @namespace, string key, IStateStore store, CancellationToken cancellationToken)
    {
        var entry = await store.GetAsync(@namespace, key, cancellationToken);
        return entry is null ? Results.NotFound() : Results.Ok(entry);
    }

    private static async Task<IResult> Create(string @namespace, [FromBody] CreateStateRequest request,
        IStateStore store, CancellationToken cancellationToken)
    {
        if (!IsValidIdentifier(request.Key, 200)) return Results.BadRequest("Invalid key.");
        if (!IsValidValue(request.Value)) return Results.BadRequest("value is required and must be at most 1 MiB of JSON.");
        var entry = await store.CreateAsync(@namespace, request.Key, request.Value, cancellationToken);
        return entry is null ? Results.Conflict("A state entry with this namespace and key already exists.") :
            Results.Created($"/api/state/{Uri.EscapeDataString(@namespace)}/{Uri.EscapeDataString(request.Key)}", entry);
    }

    private static async Task<IResult> Update(string @namespace, string key, [FromBody] UpdateStateRequest request,
        IStateStore store, CancellationToken cancellationToken)
    {
        if (!IsValidValue(request.Value)) return Results.BadRequest("value is required and must be at most 1 MiB of JSON.");
        var entry = await store.UpdateAsync(@namespace, key, request.Value, cancellationToken);
        return entry is null ? Results.NotFound() : Results.Ok(entry);
    }

    private static async Task<IResult> Delete(string @namespace, string key, IStateStore store, CancellationToken cancellationToken) =>
        await store.DeleteAsync(@namespace, key, cancellationToken) ? Results.NoContent() : Results.NotFound();
}
