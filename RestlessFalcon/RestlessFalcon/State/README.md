# JSON state API

The API targets .NET 10 and stores reusable JSON state in the separate SQL Server database selected by `ConnectionStrings:KeyValueDBConnection`. Existing sensor, charging, electricity-price and authorisation APIs keep their original routes and databases.

## Configuration and deployment

- Configure `ConnectionStrings:KeyValueDBConnection` in server configuration, environment variable `ConnectionStrings__KeyValueDBConnection`, or development user secrets. Do not commit credentials. Environment variables override `appsettings.json`.
- Existing `MasterKey` and `AuthorisationConnection` settings validate `X-Auth-Key`. All new state reads and writes require authentication. Existing API query/body auth-key inputs are unchanged.
- Use SQL Server 2016+ with database compatibility level 130 or newer. Run migrations with a separate schema-deployment identity; the runtime identity needs only SELECT, INSERT, UPDATE and DELETE on `dbo.StateEntries`.
- Install the .NET 10 SDK for builds and the .NET 10 ASP.NET Core Hosting Bundle for IIS framework-dependent hosting. A .NET 11 runtime alone does not provide default major-version roll-forward for a .NET 10 app. Restart IIS after installing the bundle.
- IDE builds targeting .NET 10 require Visual Studio 18.0 or newer. The .NET 10 SDK CLI can build the solution independently of an older Visual Studio installation.
- Publish with `dotnet publish -c Release`. Migration scripts are copied to the publish output. Apply the schema before enabling state traffic.
- The application never runs migrations automatically. The old `Startup.cs` and controllers have been replaced by `Program.cs` and Minimal API handlers. Swagger remains at `/swagger`.

## Migrations

Use SSMS or `sqlcmd` to run `Migrations/KeyValueDB/001_CreateStateEntries.sql` against the **KV database**, not any of the existing application databases. Verify the selected server/database before execution.

Example with Windows integrated authentication (substitute server/database names, not passwords):

```powershell
sqlcmd -S '<server>' -d '<kv-database>' -E -b -i '.\Migrations\KeyValueDB\001_CreateStateEntries.sql'
```

- The script creates `dbo.SchemaMigrations` and `dbo.StateEntries` transactionally. A transaction-owned application lock serializes migration execution. Reapplying a recorded migration is a no-op.
- Namespace and key form a case-sensitive primary key. Values are stored as an internal object envelope (`{"value": ...}`) so the SQL JSON constraint supports objects, arrays, numbers, strings, booleans and null. API responses return the original value, not an escaped JSON string.
- `CreatedAt` and `UpdatedAt` are UTC `datetimeoffset` values; updates preserve the creation timestamp.
- **Rollback deletes all state.** Back up the KV database and stop state writes first. Run `001_CreateStateEntries.rollback.sql` explicitly against the same database. It removes version 1 and the state table, retains the ledger and refuses to run if newer migrations remain.
- Future changes should add sequentially numbered migration/rollback scripts rather than editing an already-applied migration.

## Routes and validation

All routes start with `/api/state/{namespace}` and require a single `X-Auth-Key` header over HTTPS.

- `POST /api/state/{namespace}` with `{"key":"identifier","value":<JSON>}`: 201 and a Location header; 409 if the key exists.
- `GET /api/state/{namespace}?skip=0&take=100`: sorted key/timestamp metadata; defaults to 100 entries, maximum 1000 per page. Values are retrieved individually.
- `GET /api/state/{namespace}/{key}`: 200 with namespace, key, value, createdAt and updatedAt; 404 if missing.
- `PUT /api/state/{namespace}/{key}` with `{"value":<JSON>}`: replace an existing value, 200 with the updated entry; 404 if missing. This is not an upsert.
- `DELETE /api/state/{namespace}/{key}`: 204 if deleted; 404 if missing.
- Identifiers start with an ASCII letter/digit and may contain ASCII letters/digits, `.`, `_` and `-`. Namespace maximum length is 100, key maximum is 200. Identifiers are case-sensitive.
- Values must be explicitly present and at most 1 MiB of UTF-8 JSON. JSON null is allowed; an omitted value is not.
- Invalid input returns 400, invalid/missing auth returns 403 for otherwise well-formed requests, and database failures return a generic 500 without credentials or SQL details. Framework request parsing can return 400 before auth filters execute.
- Updates use last-write-wins semantics. There is no per-user ownership, expiry, heating execution or recurring scheduler in this API. Every valid existing application auth key has access to all state namespaces.

## Car heating windows example

Use namespace `car-heating-windows` and a key such as `family-car`. An entry can contain a list of absolute heating intervals with explicit UTC offsets:

```json
{
  "key": "family-car",
  "value": {
    "enabled": true,
    "windows": [
      {
        "start": "2026-01-20T06:30:00+02:00",
        "end": "2026-01-20T07:30:00+02:00"
      }
    ]
  }
}
```

The state store is schema-agnostic: clients should validate that each end is after its start and handle schedule/time-zone rules. This sample does not introduce a fixed recurring schedule contract.

PowerShell CRUD example (use a securely obtained key in `$authKey`; no real credentials appear below):

```powershell
$base = 'https://<your-host>/api/state/car-heating-windows'
$headers = @{ 'X-Auth-Key' = $authKey }
$value = @{ enabled = $true; windows = @(@{
    start = '2026-01-20T06:30:00+02:00'; end = '2026-01-20T07:30:00+02:00'
}) }
$body = @{ key = 'family-car'; value = $value } | ConvertTo-Json -Depth 10
Invoke-RestMethod -Method Post -Uri $base -Headers $headers -ContentType 'application/json' -Body $body
Invoke-RestMethod -Method Get -Uri "$base/family-car" -Headers $headers
Invoke-RestMethod -Method Get -Uri "${base}?skip=0&take=100" -Headers $headers
$value.enabled = $false
$body = @{ value = $value } | ConvertTo-Json -Depth 10
Invoke-RestMethod -Method Put -Uri "$base/family-car" -Headers $headers -ContentType 'application/json' -Body $body
Invoke-RestMethod -Method Delete -Uri "$base/family-car" -Headers $headers
```

## Validation

`RestlessFalcon.Validation` is a dependency-free executable validation harness included in the solution. Run these commands from the solution directory:

```powershell
# HTTP/Swagger/binding/auth/JSON checks with all real configuration removed and database access blocked.
dotnet run --project .\RestlessFalcon.Validation\RestlessFalcon.Validation.csproj

# Also verify real migration/repository behavior in a generated disposable LocalDB database.
SqlLocalDB start MSSQLLocalDB
dotnet run --project .\RestlessFalcon.Validation\RestlessFalcon.Validation.csproj -- --sql
```

The optional SQL checks use only the hard-coded `(localdb)\MSSQLLocalDB` instance with integrated authentication. They create a uniquely named database and delete it in a `finally` block. They never read the configured remote connection string. Checks cover migration reapplication, CRUD, duplicates, UTC timestamps, namespace isolation, JSON types/constraint, rollback safeguards and rollback/reapplication.

This console harness is run with `dotnet run`, not `dotnet test`. Before production deployment, verify the remote server's version, schema permissions and connectivity separately. No remote database is modified by validation, build or publish.
