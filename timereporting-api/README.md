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
applied with `dotnet ef`. The old `GMG.TimeReporting.Database` SSDT project, the
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

The schema is managed by EF Core migrations. Restore the local tools once per clone:

```cmd
dotnet tool restore
```

Create the role and the database, then point `ConnectionStrings:DefaultConnection` at it:

```sql
CREATE ROLE timereporting LOGIN PASSWORD '<a strong password>';
CREATE DATABASE timereporting OWNER timereporting;
```

```cmd
dotnet ef database update --project GMG.TimeReporting.Core --startup-project GMG.TimeReporting.WebApi --context TimeReportingContext
```

`--context` is not optional: the API registers two contexts, and only `TimeReportingContext` has
migrations.

`InitialCreate` is the whole schema: `CommonTasks`, `TimeEntries` and the `StartTime` index. It
targets PostgreSQL only — the SQL Server migrations it replaced are in git history, and there is no
path that upgrades a SQL Server database in place. Moving an existing SQL Server instance means
exporting its `CommonTasks` and `TimeEntries` rows and loading them into the new database
separately.

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
security application; EF Core migrations do not own it. `sql/passwordarchive.sql` creates the tables
this API expects, so a fresh PostgreSQL instance can be brought up with:

```cmd
psql -c "CREATE DATABASE passwordarchive OWNER timereporting"
psql -d passwordarchive -f sql/passwordarchive.sql
```

Then point `ConnectionStrings:PasswordArchiveConnection` at it.

Remaining setup:

- Give the API's PostgreSQL role permission to read and write to the database. The old EXECUTE grant
  on the stored procedures is no longer needed — there are none.
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