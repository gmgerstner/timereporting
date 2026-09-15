# Time Reporting API

The Time Reporting API records time spent on individual tasks and can be used with any timesheet system. This code is the server-side (API) logic.

This code is written in C# and SQL with Visual Studio.

The solution targets **.NET 10** and contains three projects, all `net10.0`:

| Project                      | Purpose                                              |
| ---------------------------- | ---------------------------------------------------- |
| `GMG.TimeReporting.Core`     | Entities, DbContext and EF Core migrations            |
| `GMG.TimeReporting.WebApi`   | The HTTP API                                          |
| `GMG.TimeReporting.UnitTests`| Tests                                                 |

The database is **PostgreSQL**, reached through the `Npgsql.EntityFrameworkCore.PostgreSQL`
provider. The schema is **code-first**: it lives in `GMG.TimeReporting.Core/Migrations` and is
applied at startup, or by hand with `dotnet ef`. Point the API at an empty PostgreSQL server and it
creates both databases for itself — see [Database](#database). The old `GMG.TimeReporting.Database` SSDT project, the
`GMG.TimeReporting.WinUI` WinForms client and its `GMG.TimeReporting.Library` data library have been
removed; their history is still in git, as is the SQL Server version of the schema.

Entity and column names keep their PascalCase spelling, so EF quotes them. Hand-written `psql`
queries have to quote them too: `SELECT * FROM "TimeEntries";`, not `SELECT * FROM TimeEntries;`.

Times are stored as `timestamp without time zone` and are naive local times, the same way SQL
Server's `datetime` held them: the UI posts date-times with no zone designator and the API compares
them against `DateTime.Today`. `UnspecifiedKindConverter` strips the `DateTimeKind` off every value
on its way to the database, because Npgsql — unlike SQL Server — refuses to write a UTC-kind value
to a `timestamp without time zone` column.

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
- PostgreSQL 14 or later to run against.
- To install, you will need a Windows Server running IIS with the
  [ASP.NET Core 10 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/10.0).

## Running locally

```cmd
dotnet run --project GMG.TimeReporting.WebApi
```

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

The database-backed test is skipped unless the `TIMEREPORTING_TEST_CONNECTION` environment
variable holds an Npgsql connection string for a Time Reporting database, for example
`Host=localhost;Database=timereporting;Username=postgres;Password=...`.


## Installation

### User Interface

- First set up the UI in ISS (see https://github.com/gmgerstner/timereporting-ui)

### Database

**The API provisions its own databases on startup.** All that has to exist beforehand is a
PostgreSQL server and a login role that may create databases:

```sql
CREATE ROLE timereporting LOGIN CREATEDB PASSWORD '<a strong password>';
```

Point the two connection strings at that role and start the API. `ProvisionDatabasesAsync` in
`Program.cs` then:

- creates the `timereporting` database if the server does not have it, and applies every outstanding
  migration — so a first run builds the schema and a later deploy picks up new migrations;
- creates the `passwordarchive` database from the model if it is missing (see
  [Password archive database](#password-archive-database)).

Both steps are no-ops once everything is in place, so restarts cost a couple of round trips and log
nothing. The `CREATEDB` privilege is only needed for the run that creates the databases; you can
revoke it afterwards with `ALTER ROLE timereporting NOCREATEDB` and the API will keep working.
Without it, a first run fails fast with `42501: permission denied to create database`.

`InitialCreate` is the whole Time Reporting schema: `CommonTasks`, `TimeEntries` and the `StartTime`
index. It targets PostgreSQL only — the SQL Server migrations it replaced are in git history, and
there is no path that upgrades a SQL Server database in place. Moving an existing SQL Server
instance means exporting its `CommonTasks` and `TimeEntries` rows and loading them into the new
database separately.

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

#### Password archive database

Logins are checked against a second database, `passwordarchive`, which belongs to the separate
security application. EF Core migrations do not own it, so startup uses `EnsureCreated` rather than
`Migrate`: it builds the database from `PasswordArchiveContext` when the database is missing and
does nothing at all when it already exists. That means it will not fight the security application
over a database that app created, but it also means that if this API gets there first, the archive
is created with only the tables and columns this API models. Where the security application owns
the archive, let it create the database before the API's first run.

Remaining setup:

- The API's PostgreSQL role owns the databases it creates, so it already has read and write access.
  The old EXECUTE grant on the stored procedures is no longer needed — there are none.
- Add the necessary entry to the security application for logging in (steps not included here).
- Populate a few common tasks in the CommonTasks table. The titles the retired `GetRecentTasks`
  procedure treated as favourites were: Team meeting, Admin, Daily Scrum, Qualtrax.

### API Code

- Create a folder for the API code. Recommended: Call it something similar to the UI's folder with _api appended.
- In IIS, right-click the site for the UI and select Add Application.
- Enter the following:
  - Alias: api
  - Physical path to where the .NET code will be installed.
- Update path in the file: timereporting-api\GMG.TimeReporting.WebApi\Properties\PublishProfiles\FolderProfile.pubxml
- Deploy the built code to the api folder.
	- Right-click the GMG.TimeReporting.WebApi project and select Publish
	- Click the Publish button