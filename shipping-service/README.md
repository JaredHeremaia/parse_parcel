# Shipping packaging service

Given the dimensions and weight of a package, this service advises on the **cost** and
**package type** required — or explains why no packaging solution exists.

The published price list:

| Package type | Length | Breadth | Height | Cost |
|--------------|--------|---------|--------|------|
| Small        | 200mm  | 300mm   | 150mm  | 5.00 |
| Medium       | 300mm  | 400mm   | 200mm  | 7.50 |
| Large        | 400mm  | 600mm   | 250mm  | 8.50 |

Packages over **25kg** cannot currently be moved, and anything larger than the largest
box gets no quote. Costs are plain numbers: the service prices in NZD and does not carry
a currency on the wire.

## Quick start

Runs the same on **Windows, macOS and Linux** — it is plain .NET 8 with no OS-specific
code. You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). No
database is needed: the catalogue is held in memory by default.

A newer SDK will *build* the solution — `global.json` rolls forward — but the tests run on
the `net8.0` test host, which needs the .NET 8 runtime itself. So install .NET 8 even if
you already have 9 or 10; they sit side by side. If you would rather not add a second SDK,
the [ASP.NET Core 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) is the
smallest download that covers both test projects.

```bash
dotnet --list-runtimes                        # expect a Microsoft.AspNetCore.App 8.0.x line
dotnet test                                   # run every test
dotnet run --project src/Shipping.Api         # API on http://localhost:5080
```

Then, from a second terminal:

```bash
curl http://localhost:5080/api/packages
dotnet run --project src/Shipping.ConsoleClient   # or drive it from the demo client
```

See [API](#api) for every route, [Console client](#console-client) for the demo client, and
`requests.http` for a ready-made request per endpoint including the failure cases.

Those `dotnet` commands are identical on every platform. If you prefer shorter ones,
`scripts/` has wrappers for both shells — see [Running on your platform](#running-on-your-platform).

## API

| Method | Route                       | Purpose                                          |
|--------|-----------------------------|--------------------------------------------------|
| GET    | `/api/packages`             | Every package type: size, dimensions, price      |
| GET    | `/api/packages/{id\|name}`  | One package type, by id or by name (`small`)     |
| POST   | `/api/packages`             | Add a package type                               |
| PATCH  | `/api/packages/{id}`        | Change part of a package type                    |
| DELETE | `/api/packages/{id}`        | Remove a package type                            |
| POST   | `/api/quotes`               | Advise on cost and package type for a package    |
| GET    | `/health`                   | Liveness                                         |

`requests.http` has a ready-made request for each of these, including the failure cases.

> **On Windows PowerShell**, the examples below need two adjustments. `curl` is an alias for
> `Invoke-WebRequest`, which has no `-X` parameter, so call `curl.exe` by its full name. And
> `\` is not a line continuation — either put the command on one line or use a backtick
> `` ` ``. See [Calling the API from PowerShell](#calling-the-api-from-powershell) for the
> native alternative.

**Get one package type** — by name (case-insensitive) or by id:

```bash
curl http://localhost:5080/api/packages/small
curl http://localhost:5080/api/packages/11111111-1111-1111-1111-111111111111
```

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "name": "Small",
  "dimensions": { "lengthMm": 200, "breadthMm": 300, "heightMm": 150, "volumeMm3": 9000000 },
  "cost": 5.00,
  "maxWeightKg": 25
}
```

An unknown key is a `404`:

```json
{
  "title": "Not found",
  "status": 404,
  "detail": "No package type found for 'enormous'."
}
```

**Add a package type** — responds `201 Created` with a `Location` header and the new record:

```bash
curl -X POST http://localhost:5080/api/packages \
  -H 'Content-Type: application/json' \
  -d '{"name":"Extra Large","lengthMm":500,"breadthMm":700,"heightMm":300,"cost":12.50}'
```

```json
{
  "id": "0e112428-245a-4c9c-a3cd-e942812909ea",
  "name": "Extra Large",
  "dimensions": { "lengthMm": 500, "breadthMm": 700, "heightMm": 300, "volumeMm3": 105000000 },
  "cost": 12.50,
  "maxWeightKg": 25
}
```

Names are unique, so reusing one is a `409`:

```json
{
  "title": "Conflict",
  "status": 409,
  "detail": "A package type named 'Small' already exists."
}
```

**Change part of a package type** — `PATCH` applies only the fields present in the body, so a
price change does not mean restating the dimensions:

```bash
curl -X PATCH http://localhost:5080/api/packages/11111111-1111-1111-1111-111111111111 \
  -H 'Content-Type: application/json' \
  -d '{"cost":6.25}'
```

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "name": "Small",
  "dimensions": { "lengthMm": 200, "breadthMm": 300, "heightMm": 150, "volumeMm3": 9000000 },
  "cost": 6.25,
  "maxWeightKg": 25
}
```

A body with nothing in it is a `400` rather than a silent no-op:

```json
{
  "title": "Invalid request",
  "status": 400,
  "detail": "Provide at least one of name, lengthMm, breadthMm, heightMm or cost.",
  "errors": ["Provide at least one of name, lengthMm, breadthMm, heightMm or cost."]
}
```

**Remove a package type** — by id only, and a successful delete returns `204 No Content` with an
empty body:

```bash
curl -i -X DELETE http://localhost:5080/api/packages/0e112428-245a-4c9c-a3cd-e942812909ea
```

Deleting something that is already gone is a `404`, so the call is safe to retry but not silently
idempotent:

```json
{
  "title": "Not found",
  "status": 404,
  "detail": "No package type found with id '0e112428-245a-4c9c-a3cd-e942812909ea'."
}
```

**Quoting**

```bash
curl -X POST http://localhost:5080/api/quotes \
  -H 'Content-Type: application/json' \
  -d '{"lengthMm":200,"breadthMm":300,"heightMm":150,"weightKg":5}'
```

```json
{
  "packageTypeId": "11111111-1111-1111-1111-111111111111",
  "packageType": "Small",
  "cost": 5.00,
  "dimensions": { "lengthMm": 200, "breadthMm": 300, "heightMm": 150, "volumeMm3": 9000000 },
  "weightKg": 5
}
```

When we cannot package something the response is `422` with an RFC 7807 problem document
and a machine-readable `reason` (`Overweight` or `Oversized`):

```json
{
  "title": "No packaging solution",
  "status": 422,
  "detail": "We cannot currently ship packages over 25kg (this one is 25.01kg).",
  "reason": "Overweight"
}
```

**Status codes**

| Code | When                                                              |
|------|-------------------------------------------------------------------|
| 400  | Malformed or nonsensical input (missing field, zero/negative side) |
| 404  | No such package type                                              |
| 409  | A package type with that name already exists                      |
| 422  | Valid request, but no packaging solution (too big or too heavy)    |

### Calling the API from PowerShell

`curl.exe` works exactly as above once you use the full name and keep the command on one line:

```powershell
curl.exe -X POST http://localhost:5080/api/quotes -H "Content-Type: application/json" -d '{"lengthMm":200,"breadthMm":300,"heightMm":150,"weightKg":5}'
```

`Invoke-RestMethod` is the native option, and it avoids quoting JSON by hand. It also parses
the response, so you get `.packageType` and `.cost` rather than raw text:

```powershell
$body = @{ lengthMm = 200; breadthMm = 300; heightMm = 150; weightKg = 5 } | ConvertTo-Json

Invoke-RestMethod -Method Post -Uri http://localhost:5080/api/quotes `
  -ContentType 'application/json' -Body $body
```

Note that `Invoke-RestMethod` throws on a non-2xx response rather than returning it, so reading
the body of a `404`, `409` or `422` means catching the exception:

```powershell
try { Invoke-RestMethod -Method Delete -Uri http://localhost:5080/api/packages/$id }
catch { $_.ErrorDetails.Message }
```

The same applies to every other example in this section. Alternatively `requests.http` runs each
request from VS Code, Visual Studio or Rider with no shell quoting at all, and the
[console client](#console-client) covers the same operations.

## Console client

`Shipping.ConsoleClient` calls the API over HTTP. Run it with no arguments for a tour — the
catalogue plus a quote for the sample package — or give it a command:

```
list                                                    every package type
quote  <length> <breadth> <height> <weight>             advise on cost and package type
add    <name> <length> <breadth> <height> <cost>        add a package type
update <id> [options]                                   change part of a package type
delete <id>                                             remove a package type
help                                                    show usage
```

Lengths are whole millimetres, weight is kilograms, cost is NZD. A name containing spaces
needs quoting. Arguments after `--` go to the client rather than to `dotnet run`:

```bash
dotnet run --project src/Shipping.ConsoleClient
dotnet run --project src/Shipping.ConsoleClient -- quote 201 300 150 5
dotnet run --project src/Shipping.ConsoleClient -- add "Extra Large" 500 700 300 12.50
dotnet run --project src/Shipping.ConsoleClient -- update <id> --cost 9.99
```

Update options, at least one required — anything left out keeps its current value:

```
--name <name>   --length <mm>   --breadth <mm>   --height <mm>   --cost <amount>
```

```
 _____              _        __  __
|_   _| __ __ _  __| | ___  |  \/  | ___    __ _
  | || '__/ _` |/ _` |/ _ \ | |\/| |/ _ \  /  ('>--
  | || | | (_| | (_| |  __/ | |  | |  __/  \__/
  |_||_|  \__,_|\__,_|\___| |_|  |_|\___|   L\_
   W h e r e     k i w i     l o o k     f i r s t

Package types at http://localhost:5080

  Small         200 x  300 x  150 mm     5.00 NZD
  Medium        300 x  400 x  200 mm     7.50 NZD
  Large         400 x  600 x  250 mm     8.50 NZD

Quoting 200x300x150mm at 5kg

  Package type : Small
  Cost         : 5.00 NZD
```

Writing to the catalogue reports what the API returned:

```
$ dotnet run --project src/Shipping.ConsoleClient -- add "Extra Large" 500 700 300 12.50
Added 'Extra Large'.
  Extra Large   500 x  700 x  300 mm    12.50 NZD
  de3436e9-3971-4d03-a4c6-92e29c4ff4d8
```

`update` is a `PATCH`, so it changes only what you name. Options are `--name`, `--length`,
`--breadth`, `--height` and `--cost`; at least one is required and anything left out keeps its
current value. The client omits absent fields from the JSON rather than sending nulls.

A package we cannot ship is reported with the API's machine-readable `reason` rather than a
bare status code:

```
  No packaging solution (Overweight): We cannot currently ship packages over 25kg (this one is 26kg).
```

The banner is decoration, so it is skipped when output is piped or redirected to a file and
only the results are written.

The address comes from `SHIPPING_API_URL`, defaulting to `http://localhost:5080`. It references
`Shipping.Contracts` and nothing else, so the request and response shapes cannot drift from
the API's.

**Exit codes**

| Code | When |
|------|------|
| 0 | The command succeeded. A quote of "no packaging solution" counts: the API answered. |
| 1 | Bad usage, or the API could not be reached at all. |
| 2 | The API was reached and refused the request — a duplicate name, an unknown id. |

## Running on your platform

The `dotnet` command line is identical everywhere; only shell syntax differs. Wrappers are
provided for both shells and can be run from any directory — they resolve paths relative to
themselves and pass the underlying exit code straight through.

| Task | macOS / Linux | Windows (PowerShell) |
|------|---------------|----------------------|
| Run every test | `./scripts/test.sh` | `.\scripts\test.ps1` |
| Run the API | `./scripts/run-api.sh` | `.\scripts\run-api.ps1` |
| Run the API on Postgres | `./scripts/run-api.sh --postgres` | `.\scripts\run-api.ps1 -Postgres` |

**Windows notes**

- PowerShell may block unsigned scripts, with "running scripts is disabled on this system".
  Either allow them for the current session with
  `Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass`, run one script without
  changing anything via `powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1`, or skip
  the wrappers and use the `dotnet` commands directly — they work unchanged in PowerShell,
  `cmd.exe` and Git Bash. If the files came from a zip, `Unblock-File .\scripts\*.ps1` clears
  the mark-of-the-web that blocks them even when the policy allows scripts.
- `curl` is an alias for `Invoke-WebRequest` and does not accept `-X`. See
  [Calling the API from PowerShell](#calling-the-api-from-powershell).
- Build somewhere you can write. `dotnet build` creates `bin\` and `obj\` inside every project,
  so a checkout under `C:\Program Files\` fails with access denied unless the prompt is
  elevated. Anywhere under your user profile is fine.
- If `dotnet` is not recognised after installing, the installer's `PATH` change does not reach
  already-open terminals — open a new one. On Arm64 Windows an x64 install lives at
  `C:\Program Files\dotnet\x64` rather than `C:\Program Files\dotnet`, so check which you have.
  ```
   powershell -ExecutionPolicy Bypass -File .\scripts\run-api.ps1
  ```
- Environment variables use different syntax. Where this README shows
  `Storage__Provider=Postgres dotnet run ...`, in PowerShell that is:
  ```powershell
  $env:Storage__Provider = 'Postgres'
  dotnet run --project src/Shipping.Api
  ```
- Windows Firewall may prompt the first time Kestrel binds port 5080. Allowing it on private
  networks is enough; the API only listens on localhost.
- `.gitattributes` normalises line endings, so shell scripts stay LF even on a Windows
  checkout and the tests behave identically on either.

- If the shell scripts lose their executable bit in transit (common when a zip is unpacked
  on Windows), restore it with `chmod +x scripts/*.sh`.

**macOS notes**

- Works on both Apple Silicon and Intel; the SDK and the `postgres:16-alpine` image are
  multi-architecture.
- If `dotnet` is not found after installing, add it to your `PATH`
  (`export PATH="$PATH:/usr/local/share/dotnet"`).
- No HTTPS development certificate is required — the API listens on plain HTTP on localhost,
  so there is no `dotnet dev-certs` step.

## Optional: Postgres

The catalogue can be persisted instead of held in memory.

```bash
docker compose up -d
dotnet run --project src/Shipping.Api --launch-profile postgres
```

That first line needs two separate things: a **Docker engine** to run the container, and the
**Compose v2 plugin** (`docker compose`, with a space). Docker Desktop provides both. A bare
`docker` CLI on its own provides neither — it is only a client.

| Platform | How to get both |
|----------|-----------------|
| macOS | [Docker Desktop](https://docs.docker.com/desktop/install/mac-install/), or `brew install --cask docker-desktop`. Launch it once (`open -a Docker`) so the engine starts. |
| Windows | [Docker Desktop](https://docs.docker.com/desktop/install/windows-install/), with WSL 2 as the backend. |
| Linux | [Docker Engine](https://docs.docker.com/engine/install/) plus the [Compose plugin](https://docs.docker.com/compose/install/linux/) (`docker-compose-plugin`). |

Check each half separately:

```bash
docker compose version                      # plugin: expect "Docker Compose version v2.x"
docker info --format '{{.ServerVersion}}'   # engine: expect a version, not a socket error
```

The two failures look nothing alike:

- `unknown shorthand flag: 'd' in -d` — the Compose plugin is missing, so `docker` never
  recognised `compose` as a subcommand and tried to parse `-d` itself.
- `Cannot connect to the Docker daemon` — the plugin is present but no engine is running.

The hyphenated `docker-compose` is the unmaintained v1 tool and is not what these instructions
mean.

On macOS, Homebrew's `docker` **formula** installs only the client. Alongside Docker Desktop it
can shadow Desktop's own CLI on `PATH` and then fail to find Desktop's Compose plugin, so
`docker compose` keeps failing after an apparently successful install. `brew uninstall docker`
leaves a single working CLI.

Docker is only a convenience for getting a Postgres instance. Any PostgreSQL 16 server will do —
see [If you already run Postgres locally](#if-you-already-run-postgres-locally) below.

Or set it explicitly — macOS/Linux:

```bash
Storage__Provider=Postgres \
ConnectionStrings__Packages="Host=localhost;Port=5432;Database=shipping;Username=shipping;Password=shipping" \
dotnet run --project src/Shipping.Api
```

Windows (PowerShell):

```powershell
$env:Storage__Provider = 'Postgres'
$env:ConnectionStrings__Packages = 'Host=localhost;Port=5432;Database=shipping;Username=shipping;Password=shipping'
dotnet run --project src/Shipping.Api
```

The double underscore is .NET's cross-platform separator for nested configuration keys, so
the same variable names work on every platform.

On startup the schema is created if missing and the price list is seeded. Everything else
behaves identically — only the `IPackageTypeStore` implementation changes.

### If you already run Postgres locally

`docker-compose.yml` publishes the container on **5432**, the default Postgres port. If you
already have a Postgres running there — a Homebrew `postgresql@16` service, say — the container
either fails to bind the port, or binds it and the API silently connects to *the wrong server*.
That second case shows up as:

```
Npgsql.PostgresException  28000: role "shipping" does not exist
```

The connection succeeded; it just reached a server that has no `shipping` role. Check what holds
the port with `lsof -nP -iTCP:5432 -sTCP:LISTEN` (macOS/Linux) or
`netstat -ano | findstr :5432` (Windows). Three ways out:

**Use the Postgres you already have** — no Docker needed. The connection string expects a
database, user and password all named `shipping`:

```bash
psql -U "$USER" -d postgres -c "CREATE ROLE shipping LOGIN PASSWORD 'shipping';"
psql -U "$USER" -d postgres -c "CREATE DATABASE shipping OWNER shipping;"
```

Then run the `postgres` launch profile as above; the schema and price list are created on
startup. To undo: `DROP DATABASE shipping;` then `DROP ROLE shipping;`.

**Stop the local server** for the duration — `brew services stop postgresql@16` on macOS.

**Move the container to another port**, changing the published port in `docker-compose.yml` to
`"5433:5432"` and pointing the connection string at `Port=5433`.

## Layout

```
src/
  Shipping.Core                  Domain: dimensions, package types, quoting, catalogue rules
  Shipping.Contracts             Request/response DTOs shared by the API and the client
  Shipping.Api                   Minimal API endpoints
  Shipping.ConsoleClient         Demo client, calls the API over HTTP
  Shipping.Persistence.Postgres  EF Core store (optional)
tests/
  Shipping.Core.Tests            Domain rules and boundaries
  Shipping.Api.Tests             Every endpoint over a real host
  Shipping.ConsoleClient.Tests   Argument parsing and output, over a stubbed transport
scripts/                         Bash and PowerShell wrappers for test / run-api
```

`Shipping.Core` has no external dependencies and no knowledge of HTTP or databases: the
packaging rules are testable on their own, and the API is a thin shell over them.
Expected failures (not found, conflict, no packaging solution) are returned as values via
`Result<T>` and `QuoteResult` rather than thrown, so each caller decides how to present them.

`Shipping.ConsoleClient` sits outside that: it is a consumer of the HTTP API, not of the
domain, and depends only on the wire contracts.

## Tests

```bash
dotnet test
```

Needs the .NET 8 runtime, not just a newer SDK — see [Quick start](#quick-start).

Covers the main path and the edges: exact boundary dimensions (200x300x150 is Small; one
millimetre over on any side moves up a size), exactly 25kg versus 25.01kg, packages too big
for every box, zero and negative inputs, malformed JSON, duplicate names, unknown ids, and
double deletes. The endpoint tests run against a real host in memory, and the console
client's parsing, exit codes and output are tested against a stubbed transport, so no
network or database is involved.

## Decisions and assumptions

- **Packages may be turned to fit.** A 300x150x200 package goes in the Small box, since it
  is that box in a different orientation. Comparing both sets of sides sorted descending is
  enough for an axis-aligned fit. It is configurable — `Packaging:AllowRotation=false`
  compares length to length, breadth to breadth, height to height as supplied.
- **The cheapest suitable box wins**, with volume then name breaking ties so results are
  deterministic. With the published list this is also the smallest, but adding a large,
  cheap box would not surprise a customer with a higher price.
- **Weight is checked before size**, so a package that is both too heavy and too big is
  reported as overweight — the first thing that would need to change.
- **"No packaging solution" is a 422, not a 200 with nulls or a 400.** The request was
  valid; we simply cannot ship it. The `reason` extension lets clients branch without
  parsing prose.
- **Package types are data, not an enum**, so `POST`/`PATCH`/`DELETE` are meaningful and a new
  size can be added without a deployment. Names are unique and are accepted as a lookup key.
- **Whole millimetres and `decimal` costs.** Dimensions are integers because the price list
  is expressed that way; money is never a float. Costs round to cents on the way in.
- **`EnsureCreated` rather than migrations** for the optional database, to keep this exercise
  self-contained. A long-lived service would use EF migrations.
- **The weight limit is configuration** (`Packaging:MaxWeightKg`), defaulting to 25, since
  "currently unable to move heavy packages" reads like a limit that will move.
- **Nothing is platform-specific.** No shell-outs, no registry and no path assumptions, so
  the suite passes identically on Windows, macOS and Linux. `.gitattributes` normalises line
  endings, so a CRLF or LF checkout behaves the same.

## What I would add next

Swagger/OpenAPI (left out to keep the dependency list to one optional package), structured
request logging with correlation ids, authentication on the write endpoints, optimistic
concurrency on `PATCH` so two people editing the same package type cannot silently overwrite
each other, and a `PUT` alongside it for callers that genuinely want to replace a whole record.
If quoting became hot, the catalogue is small and stable enough to cache.
