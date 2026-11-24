using System.Collections.Concurrent;
using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using RestlessFalcon.Endpoints;
using RestlessFalcon.Helpers;
using RestlessFalcon.State;

namespace RestlessFalcon.Validation;

public static class ValidationRunner
{
    private const string TestKey = "local-validation-only-not-a-production-key";
    private static int checks;

    public static async Task Main(string[] args)
    {
        var store = new MemoryStateStore();
        await using var app = global::Program.BuildApplication([], builder =>
        {
            // Clear all real settings and replace BOTH repositories. No database connection is possible.
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["MasterKey"] = TestKey });
            builder.Services.RemoveAll<IDatabaseHelper>();
            builder.Services.AddSingleton<IDatabaseHelper, GuardDatabaseHelper>();
            builder.Services.RemoveAll<IStateStore>();
            builder.Services.AddSingleton<IStateStore>(store);
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
        });
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            const string root = "/api/state/car-heating-windows";

            foreach (var (method, path, body) in new[]
            {
                (HttpMethod.Get, root, (string?)null),
                (HttpMethod.Get, root + "/car", null),
                (HttpMethod.Post, root, "{\"key\":\"car\",\"value\":{}}"),
                (HttpMethod.Put, root + "/car", "{\"value\":{}}"),
                (HttpMethod.Delete, root + "/car", null)
            })
                await Expect(client, method, path, body, HttpStatusCode.Forbidden);
            client.DefaultRequestHeaders.Add("X-Auth-Key", TestKey);
            await Expect(client, HttpMethod.Get, root, null, HttpStatusCode.Forbidden);
            client.DefaultRequestHeaders.Remove("X-Auth-Key");
            await Expect(client, HttpMethod.Get, root + "?authKey=unknown-key", null, HttpStatusCode.Forbidden);

            var created = await ExpectAuthorized(client, HttpMethod.Post, root,
                """{"key":"car","value":{"enabled":true,"windows":[{"start":"2026-01-20T06:30:00+02:00","end":"2026-01-20T07:30:00+02:00"}]}}""",
                HttpStatusCode.Created);
            Assert(created.GetProperty("value").GetProperty("enabled").GetBoolean(), "Structured JSON response");
            Assert(created.GetProperty("createdAt").GetDateTimeOffset().Offset == TimeSpan.Zero, "UTC timestamps");
            await ExpectAuthorized(client, HttpMethod.Post, root, "{\"key\":\"car\",\"value\":null}", HttpStatusCode.Conflict);
            var read = await ExpectAuthorized(client, HttpMethod.Get, root + "/car", null, HttpStatusCode.OK);
            Assert(read.GetProperty("value").GetProperty("windows").GetArrayLength() == 1, "Heating window round trip");
            var updated = await ExpectAuthorized(client, HttpMethod.Put, root + "/car", "{\"value\":false}", HttpStatusCode.OK);
            Assert(updated.GetProperty("value").ValueKind == JsonValueKind.False, "Boolean remains JSON boolean");
            Assert(created.GetProperty("createdAt").GetString() == updated.GetProperty("createdAt").GetString(), "Creation timestamp preserved");
            var keys = await ExpectAuthorized(client, HttpMethod.Get, root + "?skip=0&take=1", null, HttpStatusCode.OK);
            Assert(keys.GetArrayLength() == 1 && keys[0].GetProperty("key").GetString() == "car", "Paginated key list");
            await ExpectAuthorized(client, HttpMethod.Get, "/api/state/other/car", null, HttpStatusCode.NotFound);
            await ExpectAuthorized(client, HttpMethod.Get, root + "/Car", null, HttpStatusCode.NotFound);
            await ExpectAuthorized(client, HttpMethod.Delete, root + "/car", null, HttpStatusCode.NoContent);
            await ExpectAuthorized(client, HttpMethod.Get, root + "/car", null, HttpStatusCode.NotFound);
            await ExpectAuthorized(client, HttpMethod.Put, root + "/car", "{\"value\":{}}", HttpStatusCode.NotFound);
            await ExpectAuthorized(client, HttpMethod.Delete, root + "/car", null, HttpStatusCode.NotFound);

            foreach (var raw in new[] { "{}", "[]", "\"a string\"", "123.45", "true", "false", "null" })
            {
                using var document = JsonDocument.Parse(raw);
                var decoded = SqlStateStore.Decode(SqlStateStore.Encode(document.RootElement));
                Assert(decoded.GetRawText() == document.RootElement.GetRawText(), "SQL JSON envelope round trip: " + raw);
                var entry = await ExpectAuthorized(client, HttpMethod.Post, root,
                    "{\"key\":\"json-" + checks + "\",\"value\":" + raw + "}", HttpStatusCode.Created);
                Assert(entry.GetProperty("value").ValueKind == decoded.ValueKind, "HTTP JSON type preserved");
            }
            foreach (var (key, value) in new[]
            {
                ("weekday-morning", "0545-0123456-on"),
                ("weekday-evening", "0700-0123456-off")
            })
            {
                var entry = await ExpectAuthorized(client, HttpMethod.Post, root,
                    JsonSerializer.Serialize(new { key, value }), HttpStatusCode.Created);
                Assert(entry.GetProperty("key").GetString() == key && entry.GetProperty("value").GetString() == value,
                    "Independent string-valued heating entry: " + key);
            }
            await ExpectAuthorized(client, HttpMethod.Post, root, "{\"key\":\"missing-value\"}", HttpStatusCode.BadRequest);
            await ExpectAuthorized(client, HttpMethod.Put, root + "/car", "{}", HttpStatusCode.BadRequest);
            await ExpectAuthorized(client, HttpMethod.Post, root, "{\"key\":\"bad key\",\"value\":{}}", HttpStatusCode.BadRequest);
            await ExpectAuthorized(client, HttpMethod.Post, root, "{\"key\":\"car\",\"value\":", HttpStatusCode.BadRequest);
            await ExpectAuthorized(client, HttpMethod.Get, "/api/state/bad%20namespace/car", null, HttpStatusCode.BadRequest);
            await ExpectAuthorized(client, HttpMethod.Get, root + "?take=1001", null, HttpStatusCode.BadRequest);
            await ExpectAuthorized(client, HttpMethod.Get, root + "?skip=-1", null, HttpStatusCode.BadRequest);
            await ExpectAuthorized(client, HttpMethod.Get, root + "?take=bad", null, HttpStatusCode.BadRequest);
            Assert(!StateEndpoints.IsValidIdentifier("car\n", 200), "Reject trailing newline");
            Assert(!StateEndpoints.IsValidIdentifier(new string('a', 201), 200), "Reject oversized key");
            using (var large = JsonDocument.Parse(JsonSerializer.Serialize(new string('a', StateEndpoints.MaxValueBytes))))
                Assert(!StateEndpoints.IsValidValue(large.RootElement), "Reject oversized JSON value");

            store.FailNext = true;
            var error = await ExpectAuthorized(client, HttpMethod.Get, root, null, HttpStatusCode.InternalServerError);
            Assert(!error.ToString().Contains("sensitive-marker"), "Sanitized database failure");

            // Legacy write endpoints retain their original auth inputs.
            await Expect(client, HttpMethod.Post, "/api/Authorisation?keyId=1", "\"\"", HttpStatusCode.Forbidden);
            await Expect(client, HttpMethod.Post, "/api/Charging", "{}", HttpStatusCode.Forbidden);
            await Expect(client, HttpMethod.Post, "/api/ElectricityPrice", "[]", HttpStatusCode.Forbidden);
            await Expect(client, HttpMethod.Post, "/api/Sensor", "{}", HttpStatusCode.Forbidden);
            await Expect(client, HttpMethod.Delete, "/api/Sensor/1", null, HttpStatusCode.Forbidden);
            await Expect(client, HttpMethod.Post, "/api/SensorData?sensorName=car", "{}", HttpStatusCode.Forbidden);
            await Expect(client, HttpMethod.Get, "/api/ElectricityPrice?date=invalid", null, HttpStatusCode.BadRequest);
            await Expect(client, HttpMethod.Get, "/api/SensorData?amount=-1", null, HttpStatusCode.BadRequest);

            using var swagger = await client.GetFromJsonAsync<JsonDocument>("/swagger/v1/swagger.json");
            var paths = swagger!.RootElement.GetProperty("paths");
            foreach (var path in new[] { "/api/Authorisation", "/api/Charging", "/api/ElectricityPrice", "/api/Sensor",
                "/api/Sensor/{id}", "/api/SensorData", root.Replace("car-heating-windows", "{namespace}"), "/api/state/{namespace}/{key}" })
                Assert(paths.TryGetProperty(path, out _), "Swagger contains " + path);
            var operations = paths.GetProperty("/api/state/{namespace}/{key}");
            Assert(operations.GetProperty("get").GetProperty("parameters").EnumerateArray()
                .Any(p => p.GetProperty("name").GetString() == "authKey" && p.GetProperty("in").GetString() == "query" &&
                    p.GetProperty("required").GetBoolean()), "Swagger auth query parameter");
            Console.WriteLine($"PASS: {checks} local checks; no real database connection was possible.");
        }
        finally
        {
            await app.StopAsync();
        }
        if (args.Contains("--sql", StringComparer.Ordinal))
            await SqlMigrationValidation.RunAsync();
    }

    private static Task<JsonElement> ExpectAuthorized(HttpClient client, HttpMethod method, string path, string? body,
        HttpStatusCode expected)
    {
        var separator = path.Contains('?') ? '&' : '?';
        return Expect(client, method, path + separator + "authKey=" + Uri.EscapeDataString(TestKey), body, expected);
    }

    private static async Task<JsonElement> Expect(HttpClient client, HttpMethod method, string path, string? body, HttpStatusCode expected)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        Assert(response.StatusCode == expected, $"{method} {path}: expected {(int)expected}, got {(int)response.StatusCode}");
        if (expected == HttpStatusCode.Created) Assert(response.Headers.Location is not null, "Create Location header");
        if (string.IsNullOrWhiteSpace(text)) return default;
        try { using var json = JsonDocument.Parse(text); return json.RootElement.Clone(); }
        catch (JsonException) { return default; }
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + name);
        checks++;
    }

    private sealed class GuardDatabaseHelper(IConfiguration config) : IDatabaseHelper
    {
        public IConfiguration Config => config;
        public IDbConnection GetDatabaseConnection(string connectionString) =>
            throw new InvalidOperationException("Database connections are disabled during local validation.");
    }

    private sealed class MemoryStateStore : IStateStore
    {
        private readonly ConcurrentDictionary<(string Ns, string Key), StateEntry> entries = new();
        public bool FailNext { get; set; }
        public Task<IReadOnlyList<StateKey>> ListAsync(string ns, int skip, int take, CancellationToken ct)
        {
            if (FailNext) { FailNext = false; throw new InvalidOperationException("sensitive-marker"); }
            return Task.FromResult<IReadOnlyList<StateKey>>(entries.Values.Where(e => e.Namespace == ns)
                .OrderBy(e => e.Key, StringComparer.Ordinal).Skip(skip).Take(take)
                .Select(e => new StateKey(e.Key, e.CreatedAt, e.UpdatedAt)).ToArray());
        }
        public Task<StateEntry?> GetAsync(string ns, string key, CancellationToken ct) =>
            Task.FromResult(entries.TryGetValue((ns, key), out var entry) ? entry : null);
        public Task<StateEntry?> CreateAsync(string ns, string key, JsonElement value, CancellationToken ct)
        {
            var now = DateTimeOffset.UtcNow;
            var entry = new StateEntry(ns, key, value.Clone(), now, now);
            return Task.FromResult(entries.TryAdd((ns, key), entry) ? entry : null);
        }
        public Task<StateEntry?> UpdateAsync(string ns, string key, JsonElement value, CancellationToken ct)
        {
            if (!entries.TryGetValue((ns, key), out var existing)) return Task.FromResult<StateEntry?>(null);
            var entry = existing with { Value = value.Clone(), UpdatedAt = DateTimeOffset.UtcNow };
            return Task.FromResult(entries.TryUpdate((ns, key), entry, existing) ? entry : null);
        }
        public Task<bool> DeleteAsync(string ns, string key, CancellationToken ct) => Task.FromResult(entries.TryRemove((ns, key), out _));
    }
}
