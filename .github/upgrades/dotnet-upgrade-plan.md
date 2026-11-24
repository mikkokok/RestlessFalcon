# .NET 10 Upgrade Plan

## Execution Steps

Execute steps sequentially in the order listed.

1. Validate that a .NET 10 SDK required for this upgrade is installed on the machine and help obtain it if missing.
2. Ensure that the SDK version specified in global.json files is compatible with the .NET 10 upgrade.
3. Upgrade RestlessFalcon\RestlessFalcon.csproj to .NET 10.
   - Change the target framework, update verified NuGet versions listed below, resolve package breaking changes and build.
   - Convert hosting and all controller actions to genuine Minimal API route handlers, preserving existing routes, query/body bindings, JSON settings, auth-key validation and response contracts.
   - Replace Startup.cs and remove the converted controller files, including their base class; retain shared helpers and models as appropriate.
   - Register endpoint API exploration and retain Swagger documentation. Preserve HTTPS redirection and environment-specific exception/HSTS behavior.
   - Parameterize SQL in converted handlers instead of carrying forward string-interpolated input. Reject empty authentication keys before checking configured or stored keys; use explicit forbidden responses without relying on an unconfigured authentication scheme.
   - Add versioned SQL Server migration and rollback files for the empty key-value database, with a migration ledger, transactional execution and documented application instructions. Do not connect to or modify the remote database during this task.
   - Add a namespaced key-value JSON state table with a unique namespace/key identifier, JSON validity constraint and UTC created/updated timestamps. Use bounded and validated identifiers and parameterized database access through ConnectionStrings:KeyValueDBConnection.
   - Add authenticated basic CRUD Minimal APIs under /api/state/{namespace}: POST to create a key/value entry, GET to list keys, GET /{key} to read, PUT /{key} to update an existing entry, and DELETE /{key} to delete. Preserve JSON types on reads; return 409 for duplicate creates, 404 for missing entries, 400 for invalid input and 403 for invalid authentication. Use an X-Auth-Key header for the new APIs, without exposing keys or connection strings in responses or logs.
   - Support the initial car heating windows use case by documenting the car-heating-windows namespace and a JSON example containing time-zone-aware start/end timestamps. Keep the value schema general for future state storage; do not implement heating control or scheduling execution.
   - Document migration application/rollback, CRUD examples, configuration and server deployment prerequisites. A .NET 11 runtime alone is not a substitute for the .NET 10 ASP.NET Core Hosting Bundle for default framework-dependent deployment.
4. Validate RestlessFalcon\RestlessFalcon.csproj after all framework and feature changes.
   - Build the solution and validate route registration, Swagger generation, request binding, invalid-auth responses and JSON round trips where possible without external database access.
   - No existing test projects were discovered. Add or use local automated validation where practical and explicitly report database integration checks that require an isolated SQL Server database and were not run.

## Settings

### Source control

- Source branch: feature/new-db-and-minimal-api.
- Upgrade target branch: feature/new-db-and-minimal-api (existing user-created branch, checked out; do not create another branch).
- No pending changes at branch checkout.

### Excluded projects

- None.

### Project upgrade details

#### RestlessFalcon\RestlessFalcon.csproj modifications

Project properties changes:
- Target framework should be changed from `net8.0` to `net10.0`.

NuGet packages changes:
- User requested package updates; versions verified for net10.0:
  - Dapper: 2.1.66 to 2.1.100.
  - Microsoft.Data.SqlClient: 6.1.1 to 7.1.2.
  - Swashbuckle.AspNetCore: 9.0.3 to 11.0.1; adapt OpenAPI API changes as needed.
- Any additional package or version must be verified as available and compatible before use.

Feature upgrades:
- Move hosting from WebHost/Startup to WebApplication and replace controllers with Minimal APIs.
- Preserve GET/POST /api/Authorisation, GET/POST /api/Charging, GET/POST /api/ElectricityPrice, GET/POST /api/Sensor, DELETE /api/Sensor/{id}, and GET/POST /api/SensorData.
- Preserve existing endpoint auth-key inputs. Protect all new state operations using the existing AuthKeyHelper with explicit nonempty-key validation.
- Add reusable JSON key-value state storage and CRUD using the separate KeyValueDBConnection SQL Server database.
- Provide transactional, versioned schema migration/rollback files; do not apply them to the remote database.
- Document car heating windows as the first JSON-state use case, without assuming a recurring schedule schema.

Other changes:
- Update documentation and validation for .NET 10 deployment and the new APIs.
- Never include connection strings, master keys or other secrets in generated documentation or reports.
