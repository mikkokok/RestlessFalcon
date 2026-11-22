using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using RestlessFalcon.State;
using System.Text.Json;

namespace RestlessFalcon.Validation;

public static class SqlMigrationValidation
{
    public static async Task RunAsync()
    {
        // Hard-coded LocalDB endpoint: never consume the application's remote connection string.
        var database = "rf_validation_" + Guid.NewGuid().ToString("N");
        var connection = new SqlConnectionStringBuilder
        {
            DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master",
            IntegratedSecurity = true, Encrypt = SqlConnectionEncryptOption.Optional
        };
        await using var master = new SqlConnection(connection.ConnectionString);
        await master.OpenAsync();
        await master.ExecuteAsync($"CREATE DATABASE [{database}]");
        var checks = 0;
        try
        {
            connection.InitialCatalog = database;
            await using (var db = new SqlConnection(connection.ConnectionString))
            {
                await db.OpenAsync();
                var folder = Path.Combine(AppContext.BaseDirectory, "Migrations", "KeyValueDB");
                var apply = await File.ReadAllTextAsync(Path.Combine(folder, "001_CreateStateEntries.sql"));
                var rollback = await File.ReadAllTextAsync(Path.Combine(folder, "001_CreateStateEntries.rollback.sql"));
                await db.ExecuteAsync(apply);
                await db.ExecuteAsync(apply);
                Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.SchemaMigrations WHERE Version=1") == 1,
                    "Migration/reapplication ledger", ref checks);
                var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["ConnectionStrings:KeyValueDBConnection"] = connection.ConnectionString }).Build();
                var store = new SqlStateStore(config);
                var ct = CancellationToken.None;
                foreach (var raw in new[] { "{}", "[]", "\"string\"", "12.5", "true", "false", "null" })
                {
                    var key = "type-" + checks;
                    using var json = JsonDocument.Parse(raw);
                    var created = await store.CreateAsync("car-heating-windows", key, json.RootElement, ct);
                    Check(created is not null && created.Value.GetRawText() == raw, "SQL create JSON type", ref checks);
                    Check(created!.CreatedAt.Offset == TimeSpan.Zero && created.UpdatedAt.Offset == TimeSpan.Zero,
                        "SQL UTC timestamps", ref checks);
                    var read = await store.GetAsync("car-heating-windows", key, ct);
                    Check(read!.Value.GetRawText() == raw, "SQL read JSON type", ref checks);
                    Check(await store.CreateAsync("car-heating-windows", key, json.RootElement, ct) is null,
                        "SQL duplicate conflict", ref checks);
                }
                using var value = JsonDocument.Parse("{\"windows\":[{\"start\":\"2026-01-20T06:30:00+02:00\",\"end\":\"2026-01-20T07:30:00+02:00\"}]}");
                var entry = await store.CreateAsync("car-heating-windows", "car", value.RootElement, ct);
                var updated = await store.UpdateAsync("car-heating-windows", "car", value.RootElement, ct);
                Check(updated is not null && updated.CreatedAt == entry!.CreatedAt && updated.UpdatedAt >= entry.UpdatedAt,
                    "SQL update preserves creation timestamp", ref checks);
                Check(await store.GetAsync("other", "car", ct) is null, "SQL namespace isolation", ref checks);
                Check(await store.GetAsync("car-heating-windows", "Car", ct) is null, "SQL case-sensitive keys", ref checks);
                Check((await store.ListAsync("car-heating-windows", 0, 2, ct)).Count == 2, "SQL pagination", ref checks);
                Check(await store.DeleteAsync("car-heating-windows", "car", ct), "SQL delete", ref checks);
                Check(!await store.DeleteAsync("car-heating-windows", "car", ct), "SQL missing delete", ref checks);
                Check(await store.UpdateAsync("car-heating-windows", "car", value.RootElement, ct) is null,
                    "SQL missing update", ref checks);
                var invalidJsonRejected = false;
                try
                {
                    await db.ExecuteAsync("INSERT INTO dbo.StateEntries ([Namespace], [Key], ValueJson) VALUES ('test','bad-json','not-json')");
                }
                catch (SqlException ex) when (ex.Number == 547) { invalidJsonRejected = true; }
                Check(invalidJsonRejected, "SQL JSON validity constraint", ref checks);

                await db.ExecuteAsync("INSERT INTO dbo.SchemaMigrations ([Version], [Name]) VALUES (2, 'validation-only')");
                var newerVersionRejected = false;
                try { await db.ExecuteAsync(rollback); }
                catch (SqlException ex) when (ex.Number == 50003) { newerVersionRejected = true; }
                Check(newerVersionRejected, "Rollback refuses newer migrations", ref checks);
                Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.StateEntries") > 0,
                    "Failed rollback preserves state", ref checks);
                await db.ExecuteAsync("DELETE FROM dbo.SchemaMigrations WHERE [Version]=2");
                await db.ExecuteAsync(rollback);
                await db.ExecuteAsync(rollback);
                Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.SchemaMigrations") == 0,
                    "Rollback clears version", ref checks);
                Check(await db.ExecuteScalarAsync<int>("SELECT CASE WHEN OBJECT_ID('dbo.StateEntries','U') IS NULL THEN 1 ELSE 0 END") == 1,
                    "Rollback drops state table", ref checks);
                await db.ExecuteAsync(apply);
                Check(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.StateEntries") == 0,
                    "Migration applies again after rollback", ref checks);
            }
            Console.WriteLine($"PASS: {checks} SQL migration/repository checks against disposable LocalDB database.");
        }
        finally
        {
            SqlConnection.ClearAllPools();
            // The database identifier is generated locally, never derived from configuration/user input.
            await master.ExecuteAsync($"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]");
        }
    }

    private static void Check(bool success, string message, ref int checks)
    {
        if (!success) throw new InvalidOperationException("FAILED: " + message);
        checks++;
    }
}
