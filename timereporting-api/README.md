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

The app is **multi-user**. Every time entry and every favourite task belongs to a row in
`Users`, and each signed-in user only ever reads and writes their own. Users flagged
`IsAdmin` may additionally *read* anyone's schedule and timesheet; nothing lets anyone write
to another user's entries. See [Users and admins](#users-and-admins).

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

On a Development machine, startup builds a local stand-in password archive if there is not one
already and seeds two log-ins into it — `admin` / `admin` (an admin) and `user` / `user` (a plain
account) — so a fresh clone is usable without a copy of the security application's database.
This is the only thing in the app that writes to the archive at all, and the only environment it
happens in; see [Password archive database](#password-archive-database).

It also creates the matching `Users` rows up front — including `IsAdmin` on `admin` — rather
than waiting for each account's first sign-in, so the admin's "Viewing" picker has someone in it
straight away.

It seeds credentials; it does not weaken the check on them. The password is verified against the
archive in every environment and there is no bypass flag. It also stops at the first sign of a
real archive: an existing database is never created over, one that already holds a Time
Reporting entry gets no new credentials, and the `Users` rows are only created for an archive
this seeder built — recognised by the `development` system user that owns its rows — so
pointing a Development run at the real archive grants nobody anything.

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

**The API provisions its own database on startup** — the Time Reporting one. The password
archive is not its to create; see
[Password archive database](#password-archive-database). All that has to exist beforehand is a
PostgreSQL server and a login role that may create databases:

```sql
CREATE ROLE timereporting LOGIN CREATEDB PASSWORD '<a strong password>';
```

Point `ConnectionStrings:DefaultConnection` at that role, point
`ConnectionStrings:PasswordArchiveConnection` at the security application's existing archive
(whatever credentials that application expects, so long as they can read it), and start the API.
`ProvisionDatabasesAsync` in `Program.cs` then:

- creates the `timereporting` database if the server does not have it, and applies every outstanding
  migration — so a first run builds the schema and a later deploy picks up new migrations;
- checks that the `passwordarchive` database can be reached, and warns if it cannot. It is never
  created or written to outside Development (see
  [Password archive database](#password-archive-database)).

Both steps are no-ops once everything is in place, so restarts cost a couple of round trips and log
nothing.

`CREATEDB` is only needed for the run that creates the Time Reporting database; without it, a
first run fails fast with `42501: permission denied to create database`. Afterwards you can
revoke it — `ALTER ROLE timereporting NOCREATEDB` — and the API keeps working. Access to the
archive is only ever read access; granting this role anything more than `SELECT` on it is
unnecessary.

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

Logins are checked against a second database, `passwordarchive`, which belongs to a separate
web application. **Time Reporting only reads from it.** It never creates it, never migrates it
and never writes to it — point
`ConnectionStrings:PasswordArchiveConnection` at the security application's existing database
and leave the schema to that application. If the database cannot be reached, startup logs a
warning and logins fail; nothing is created to paper over it.

That rule is enforced rather than merely intended. `PasswordArchiveContext` overrides
`SaveChanges`/`SaveChangesAsync` to throw, and reads run with `QueryTrackingBehavior.NoTracking`
so there is nothing tracked for a stray save to flush. `PasswordArchiveTests` pins both.

The single exception is local development, described under [Running locally](#running-locally):
on a Development machine with no archive to borrow, startup builds a local stand-in and seeds
two log-ins into it, through the one clearly-named escape hatch
(`SaveDevelopmentSeedDataAsync`). It creates nothing if the database already exists and writes
nothing if the archive already holds a Time Reporting entry, so pointing a Development run at a
real archive leaves it untouched too.

Remaining setup:

- The API's PostgreSQL role owns the databases it creates, so it already has read and write access.
  The old EXECUTE grant on the stored procedures is no longer needed — there are none.
- Add each person's entry to the security application for logging in (steps not included here),
  then see [Users and admins](#users-and-admins) below.
- Optionally populate each user's favourites in `CommonTasks`. They are per-user now, so the rows
  need a `UserId`. The titles the retired `GetRecentTasks` procedure treated as favourites were:
  Team meeting, Admin, Daily Scrum, Qualtrax.

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