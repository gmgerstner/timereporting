# Time Reporting API

The Time Reporting API records time spent on individual tasks and can be used with any timesheet system. This code is the server-side (API) logic.

This code is written in C# and SQL with Visual Studio.

The solution targets **.NET 10** and contains three projects, all `net10.0`:

| Project                      | Purpose                                              |
| ---------------------------- | ---------------------------------------------------- |
| `GMG.TimeReporting.Core`     | Entities, DbContext and EF Core migrations            |
| `GMG.TimeReporting.WebApi`   | The HTTP API                                          |
| `GMG.TimeReporting.UnitTests`| Tests                                                 |

The API uses two databases on two different engines. **Time Reporting is PostgreSQL**, reached
through the `Npgsql.EntityFrameworkCore.PostgreSQL` provider; its schema is **code-first**, lives in
`GMG.TimeReporting.Core/Migrations` and is applied at startup, or by hand with `dotnet ef`. The
**password archive stays on SQL Server**, reached through `Microsoft.EntityFrameworkCore.SqlServer`:
it belongs to the separate security application, which owns its schema — so only Time Reporting
moved to PostgreSQL, and this API never creates or alters the archive. Point the API at the two
servers and it provisions the Time Reporting database for itself — see [Database](#database). The old `GMG.TimeReporting.Database` SSDT project, the
`GMG.TimeReporting.WinUI` WinForms client and its `GMG.TimeReporting.Library` data library have been
removed; their history is still in git, as is the SQL Server version of the Time Reporting schema.

The app is **multi-user**. Every time entry and every favourite task belongs to a row in
`Users`, and each signed-in user only ever reads and writes their own. Users flagged
`IsAdmin` may additionally *read* anyone's schedule and timesheet; nothing lets anyone write
to another user's entries. See [Users and admins](#users-and-admins).

In the Time Reporting database, entity and column names keep their PascalCase spelling, so EF quotes
them. Hand-written `psql` queries have to quote them too: `SELECT * FROM "TimeEntries";`, not
`SELECT * FROM TimeEntries;`.

Time Reporting stores times as `timestamp without time zone`, naive local times, the same way SQL
Server's `datetime` held them: the UI posts date-times with no zone designator and the API compares
them against `DateTime.Today`. `UnspecifiedKindConverter` strips the `DateTimeKind` off every value
on its way to that database, because Npgsql — unlike SQL Server — refuses to write a UTC-kind value
to a `timestamp without time zone` column. The password archive keeps its `datetime` columns and
needs no converter, since SQL Server ignores the `Kind`.

The two stored procedures that lived in the SSDT project were ported to C# in
`GMG.TimeReporting.Core/TimeReportingData/TimeReportingContext.Queries.cs`, so the database no longer
needs them:

| Stored procedure        | Replacement                                              |
| ----------------------- | -------------------------------------------------------- |
| `dbo.GetDailyTimesheet` | `TimeReportingContext.GetDailyTimesheetAsync`             |
| `dbo.GetRecentTasks`    | `TimeReportingContext.GetRecentTasksAsync`                |

`GetDailyTimesheetAsync` is covered by `TimesheetTests`, which pin the original procedure's rules:
an entry with no end time is closed off by the next entry's start, the last entry of the day stays
open and contributes no hours, and `SUM` skips nulls unless every value is null.

## Requirements

- .NET 10 SDK to build.
- PostgreSQL 14 or later for the Time Reporting database.
- SQL Server 2016 or later for the password archive database.
- To install, you will need a Windows Server running IIS with the
  [ASP.NET Core 10 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/10.0).

## Running locally

```cmd
dotnet run --project GMG.TimeReporting.WebApi
```

Logging in needs a reachable `PasswordArchive` database, in Development as much as anywhere else —
the app never creates or seeds one, so point `ConnectionStrings:PasswordArchiveConnection` at the
security application's database (or a copy of it you have made yourself) before you expect a
sign-in to work. See [Password archive database](#password-archive-database). Startup warns if it
cannot reach the archive; the API still runs, but logins will fail.

A `Users` row in the Time Reporting database is created on each account's first successful sign-in.
`IsAdmin` is not set that way — flag an admin directly in the database once the row exists.

Swagger UI is served at `/swagger`. The `http` and `https` launch profiles are in
`GMG.TimeReporting.WebApi/Properties/launchSettings.json`; the `https` profile listens on
`https://localhost:44352`, which is what the UI's `.env.development` expects.

### Configuration

`appsettings.json` holds the connection strings and the JWT signing key. Do not commit real
secrets — override them locally with
[user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) or environment
variables:

```cmd
dotnet user-secrets set "Jwt:Key" "<a long random value>" --project GMG.TimeReporting.WebApi
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<connection string>" --project GMG.TimeReporting.WebApi
```

### Tests

```cmd
dotnet test GMG.TimeReporting.UnitTests
```

The database-backed tests — including `MultiUserTests`, which pins that one user's queries
cannot see another's rows — are skipped unless the `TIMEREPORTING_TEST_CONNECTION`
environment variable holds an Npgsql connection string for a Time Reporting database, for
example `Host=localhost;Database=timereporting;Username=postgres;Password=...`. They create
their own users with randomised names and delete them afterwards, so they are safe to point
at a database that already has real users in it.


## Installation

### User Interface

- First set up the UI in ISS (see https://github.com/gmgerstner/timereporting-ui)

### Database

**The API provisions the Time Reporting database on startup, and only that one.** On PostgreSQL,
create a login role that may create databases:

```sql
CREATE ROLE timereporting LOGIN CREATEDB PASSWORD '<a strong password>';
```

`MigrateTimeReportingDatabaseAsync` in `Program.cs` then creates the `timereporting` database if the
server does not have it and applies every outstanding migration, so a first run builds the schema
and a later deploy picks up new migrations. It also checks that the archive can be reached and warns
if it cannot, so a wrong connection string surfaces at boot rather than at someone's first login.
Both steps are no-ops once everything is in place, so restarts cost a couple of round trips and log
nothing.

`CREATEDB` is only needed for the run that creates the Time Reporting database; without it, a first
run fails fast with `42501: permission denied to create database`. Afterwards you can revoke it —
`ALTER ROLE timereporting NOCREATEDB` — and the API keeps working.

**The `PasswordArchive` database on SQL Server has to exist before the API starts.** Nothing in this
API creates it, and access to it is only ever read access — see
[Password archive database](#password-archive-database).

`InitialCreate` is the whole Time Reporting schema: `CommonTasks`, `TimeEntries` and the `StartTime`
index. It targets PostgreSQL only — the SQL Server migrations it replaced are in git history, and
there is no path that upgrades a SQL Server database in place. Moving an existing SQL Server
instance means exporting its `CommonTasks` and `TimeEntries` rows and loading them into the new
database separately.

#### Upgrading a database created before multi-user

`AddUsers` puts a `NOT NULL` `UserId` on `TimeEntries` and `CommonTasks` with a foreign key to
the new `Users` table. Existing rows have no owner to point at, so on a database that already
holds entries the migration stops with:

```
23503: insert or update on table "TimeEntries" violates foreign key constraint
```

It runs in a transaction and rolls back completely, leaving the database exactly as it was —
no half-applied schema. Since no data is being carried across, clear the old rows (or drop the
database and let startup rebuild it) and run it again:

```sql
DELETE FROM "TimeEntries";
DELETE FROM "CommonTasks";
```

#### Applying migrations by hand

If you would rather control when the schema changes, the same migrations apply from the command
line. Restore the local tools once per clone:

```cmd
dotnet tool restore
```

```cmd
dotnet ef database update --project GMG.TimeReporting.Core --startup-project GMG.TimeReporting.WebApi --context TimeReportingContext
```

`--context` is not optional: the API registers two contexts, and only `TimeReportingContext` has
migrations.

To generate a script instead of connecting (for a DBA to review and run):

```cmd
dotnet ef migrations script --idempotent --project GMG.TimeReporting.Core --startup-project GMG.TimeReporting.WebApi --context TimeReportingContext --output deploy.sql
```

To change the schema, edit the entities and `OnModelCreating`, then:

```cmd
dotnet ef migrations add <Name> --project GMG.TimeReporting.Core --startup-project GMG.TimeReporting.WebApi --context TimeReportingContext
```

#### Users and admins

Logging in checks the supplied password against the PasswordArchive row whose `URL` is
`https://timereporting.gmgdesk.com`, matching the username case-insensitively. Nothing in
this app creates those credentials — add them through the security application.

The Time Reporting database keeps its own small `Users` table alongside. It holds no
credentials: just a `UserId` for the rest of the schema to key off, the (lower-cased)
username, and `IsAdmin`. **The row is created automatically on first successful login**, so
adding a person is only ever a matter of giving them archive credentials and having them
sign in once.

New users are never admins. Promote one afterwards:

```sql
UPDATE "Users" SET "IsAdmin" = true WHERE "Username" = 'someone';
```

An admin may read any user's schedule and timesheet — the UI shows them a "Viewing" picker —
and may list users through `Users/GetUsers`. Both work off the `Users` table, so **a person
shows up in the picker once they have signed in at least once**, which is the moment their row
is created. Someone who has never logged in has no account here and no entries to look at; if a
colleague is missing from the list, that is why. That is the whole of it: the write endpoints
resolve an entry by id *and* owner, so an admin editing or deleting someone else's entry gets
the same 404 as anyone else. Admin is not a way to clock time on another person's behalf.

#### Password archive database

Logins are checked against a second database, `PasswordArchive`, which lives on **SQL Server** and
belongs to the separate security application. That application owns the database and its schema
outright, which is why the archive stayed on SQL Server when Time Reporting moved to PostgreSQL.

**This API only reads the archive.** It never creates it, never migrates it and never writes to it.
Point `ConnectionStrings:PasswordArchiveConnection` at the security application's existing database
and leave the schema to that application. If the database cannot be reached, startup logs a warning
and logins fail; nothing is created to paper over it.

That rule is enforced rather than merely intended. `PasswordArchiveContext` overrides
`SaveChanges`/`SaveChangesAsync` to throw, and reads run with `QueryTrackingBehavior.NoTracking` so
there is nothing tracked for a stray save to flush. `PasswordArchiveTests` pins both, without
needing a database. `OnModelCreating` describes the security application's existing schema so EF can
query it — it is not a definition this API is entitled to apply. If the two ever drift, the fix is
to update `PasswordArchiveContext` to match the real database, never the other way round.

There is no Development exception: a local run needs a real archive to point at, the same as any
other environment.

Remaining setup:

- The API's PostgreSQL role owns the Time Reporting database it creates, so it already has read and
  write access. The old EXECUTE grant on the stored procedures is no longer needed — there are none.
- On SQL Server, the API's login needs only read access to `PasswordArchive`. `db_datareader` is
  enough; it never writes, and no schema-altering rights are wanted.
- Add each person's entry to the security application for logging in (steps not included here),
  then see [Users and admins](#users-and-admins) below.
- Optionally populate each user's favourites in `CommonTasks`. They are per-user now, so the rows
  need a `UserId`. The titles the retired `GetRecentTasks` procedure treated as favourites were:
  Team meeting, Admin, Daily Scrum, Qualtrax.

### API Code

The API is an IIS sub-application of the UI's site, living in an `api` folder inside the UI's
own directory. One-time setup:

- In IIS, right-click the site for the UI and select Add Application.
- Enter the following:
  - Alias: `api`
  - Physical path: the `api` folder inside the UI's physical path — `ApiDeployDir` in the
    repository's `Directory.Build.props`, by default
    `\\DARMIK\Web\gmgdesk.com\timereporting\api`. Create the folder if it does not exist yet.
- Set the application pool's environment variables for the connection strings and the JWT
  signing key (see [Configuration](#configuration)).

To deploy, run this from the repository root — it publishes the API *and* the UI:

```cmd
dotnet msbuild deploy.proj
```

To push a server-side fix without rebuilding the UI:

```cmd
dotnet msbuild deploy.proj -t:DeployApi
```

Either command takes the API offline before it copies, and brings it back afterwards. The API
runs in-process, so `w3wp` holds its assemblies open and publishing over a running application
fails partway through, leaving a folder that is missing whichever files were still locked. The
deploy writes `app_offline.htm` into `ApiDeployDir` first, which the ASP.NET Core Module
answers by shutting the application down and releasing them; deleting the file lets the app
start again on the next request. A failed publish deletes it too, so a broken deploy never
leaves the API switched off. If a deploy is killed outright, check for a stray
`app_offline.htm` — while it is there, every API call returns 503.

The deploy then checks that `web.config` and the application assembly actually arrived, and
fails loudly if either is missing. `web.config` is what maps the `/api` application to
`AspNetCoreModuleV2`; without it IIS serves the folder as static content and answers every API
call with its own `404 - File or directory not found`, which produces no ASP.NET Core error
page and nothing in the application log. It looks exactly like a broken UI rather than a
missing file, which is why the deploy checks rather than trusts.

Publishing from Visual Studio — right-click GMG.TimeReporting.WebApi, Publish, `FolderProfile`
— still works and writes to the same `ApiDeployDir`, so the two routes cannot disagree about
the destination. It does neither of the steps above, though: prefer the command line for a
server that is up. Change the destination in `Directory.Build.props`, never in the `.pubxml`.

`web.config` is generated by the publish, merged with
`GMG.TimeReporting.WebApi/web.config` in the project. That file exists only to clear inherited
URL rewrite rules — see [IIS rewrite rules](#iis-rewrite-rules).


### IIS rewrite rules

The API is a child application of the UI's site, and IIS copies a parent site's
`system.webServer` into every child application. The UI's `web.config` ends with a catch-all
SPA rule — anything that is not a file on disk is rewritten to `/` — and inherited into this
application that rule would swallow the whole API.

The reason it would is that an inherited rule matches against the URL *relative to the
application it is running in*. A request for `/api/Authentication/Login` is seen inside this
application as `Authentication/Login`, so the UI's first rule (`^api/`, which exists to let API
requests through) no longer matches it, and it falls into the catch-all. So
`GMG.TimeReporting.WebApi/web.config` clears the rewrite rules it inherits. Clearing them is
free — this application has no use for any rule the UI defines.

**Do not "fix" this by editing the UI's rules instead.** Both of the UI's rules are gated on
`<add input="{HTTPS}" pattern="on" />`, and Cloudflare terminates TLS in front of this site and
talks to the origin over plain HTTP. `{HTTPS}` is therefore `off` at the origin and **neither
rule has ever fired in production**. That has two consequences worth knowing before touching
the file:

- The inherited-rules problem above is latent rather than active. The `<clear />` is there so
  it stays that way if the origin ever serves HTTPS directly, or Cloudflare is switched to a
  Full SSL mode.
- SPA deep links do not work on a cold load. `https://timereporting.gmgdesk.com/login` typed
  into the address bar is a 404; the app only routes correctly once `/` has loaded it.

Removing the `{HTTPS}` condition to fix that second point takes the whole site down. The
catch-all rewrites to `/`, and `/` itself matches `^(.*)`, so URL Rewrite rewrites it again and
again until it gives up — an infinite rewrite loop, which IIS reports as a bare `500` on every
request with nothing useful in it. A catch-all that is actually enabled has to rewrite to
`/index.html` rather than `/`, and exclude `/index.html` and `^api/` in its conditions, so that
the second pass finds a real file and stops.


## Logging

Logging goes through **Serilog** (`Serilog.AspNetCore`), configured entirely from the `Serilog`
section of `appsettings.json` rather than in code, so levels and sinks can be changed on the
server and picked up by a restart. It replaces the default ASP.NET Core logging providers — the
old `Logging:LogLevel` section is gone and setting it has no effect.

Two sinks are written:

| Sink    | Where it goes                                                                 |
| ------- | ----------------------------------------------------------------------------- |
| Console | stdout — visible under `dotnet run`, and captured by IIS only if stdout logging is enabled |
| File    | `logs/timereporting-api-<date>.log` under the application's folder, one file per day, 20 MB per file, the last 31 kept |

The file path is relative to the content root, so on the server the logs land in a `logs` folder
inside `ApiDeployDir` (by default `\\DARMIK\Web\gmgdesk.com\timereporting\api\logs`). **The
application pool identity needs write access to that folder.** If it cannot write there, Serilog
drops the file sink silently — the app still starts and still logs to the console, which is easy
to mistake for "nothing is being logged". The folder is created on first write; `logs` is in
`.gitignore`, so local runs leave nothing to commit.

`UseSerilogRequestLogging` collapses each request into a single summary line — method, path,
status code, elapsed milliseconds, and the authenticated username where there is one — instead
of the several lines ASP.NET Core writes per request. Requests that arrive unauthenticated, and
those whose token fails validation, log a null `User`; that is expected, not a gap.

`Program.cs` sets up a bootstrap logger before the host is built and wraps startup in
`try`/`catch`/`finally`, so a failure that happens before configuration is read — a missing
`Jwt:Key`, an unreachable database — is logged as `Fatal` and flushed rather than vanishing with
the process. `HostAbortedException` is excluded because that is how `dotnet ef` stops the host
after reading the `DbContext`; it is not an error.

To change what is recorded without redeploying, edit `Serilog:MinimumLevel` in the deployed
`appsettings.json` and restart the application pool. `MinimumLevel:Override` is keyed by logger
name prefix — for instance, setting `Microsoft.EntityFrameworkCore.Database.Command` to
`Information` logs every SQL statement EF issues, which is useful for a short diagnostic window
and far too noisy to leave on. Development already runs at `Debug` with that override in place,
in `appsettings.Development.json`.
