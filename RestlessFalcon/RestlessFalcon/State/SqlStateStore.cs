using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace RestlessFalcon.State;

public sealed class SqlStateStore(IConfiguration configuration) : IStateStore
{
    private SqlConnection CreateConnection() => new(configuration.GetConnectionString("KeyValueDBConnection")
        ?? throw new InvalidOperationException("KeyValueDBConnection is not configured."));

    public async Task<IReadOnlyList<StateKey>> ListAsync(string ns, int skip, int take, CancellationToken cancellationToken)
    {
        await using var conn = CreateConnection();
        var rows = await conn.QueryAsync<StateRow>(new CommandDefinition("""
            SELECT [Key], CreatedAt, UpdatedAt FROM dbo.StateEntries
            WHERE [Namespace] = @ns ORDER BY [Key]
            OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY
            """, new { ns, skip, take }, cancellationToken: cancellationToken));
        return rows.Select(row => new StateKey(row.Key, row.CreatedAt, row.UpdatedAt)).ToArray();
    }

    public async Task<StateEntry?> GetAsync(string ns, string key, CancellationToken cancellationToken)
    {
        await using var conn = CreateConnection();
        var row = await conn.QuerySingleOrDefaultAsync<StateRow>(new CommandDefinition("""
            SELECT [Namespace], [Key], ValueJson, CreatedAt, UpdatedAt FROM dbo.StateEntries
            WHERE [Namespace] = @ns AND [Key] = @key
            """, new { ns, key }, cancellationToken: cancellationToken));
        return ToEntry(row);
    }

    public async Task<StateEntry?> CreateAsync(string ns, string key, JsonElement value, CancellationToken cancellationToken)
    {
        await using var conn = CreateConnection();
        try
        {
            var row = await conn.QuerySingleAsync<StateRow>(new CommandDefinition("""
                INSERT INTO dbo.StateEntries ([Namespace], [Key], ValueJson)
                OUTPUT inserted.[Namespace], inserted.[Key], inserted.ValueJson, inserted.CreatedAt, inserted.UpdatedAt
                VALUES (@ns, @key, @json)
                """, new { ns, key, json = Encode(value) }, cancellationToken: cancellationToken));
            return ToEntry(row);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            return null;
        }
    }

    public async Task<StateEntry?> UpdateAsync(string ns, string key, JsonElement value, CancellationToken cancellationToken)
    {
        await using var conn = CreateConnection();
        var row = await conn.QuerySingleOrDefaultAsync<StateRow>(new CommandDefinition("""
            UPDATE dbo.StateEntries SET ValueJson = @json, UpdatedAt = TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')
            OUTPUT inserted.[Namespace], inserted.[Key], inserted.ValueJson, inserted.CreatedAt, inserted.UpdatedAt
            WHERE [Namespace] = @ns AND [Key] = @key
            """, new { ns, key, json = Encode(value) }, cancellationToken: cancellationToken));
        return ToEntry(row);
    }

    public async Task<bool> DeleteAsync(string ns, string key, CancellationToken cancellationToken)
    {
        await using var conn = CreateConnection();
        return await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM dbo.StateEntries WHERE [Namespace] = @ns AND [Key] = @key",
            new { ns, key }, cancellationToken: cancellationToken)) != 0;
    }

    // An object envelope lets SQL Server 2016+ ISJSON validate scalars and JSON null too.
    public static string Encode(JsonElement value) => JsonSerializer.Serialize(new { value });

    public static JsonElement Decode(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("value").Clone();
    }

    private static StateEntry? ToEntry(StateRow? row) => row is null ? null :
        new(row.Namespace, row.Key, Decode(row.ValueJson), row.CreatedAt, row.UpdatedAt);

    private sealed class StateRow
    {
        public string Namespace { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string ValueJson { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
