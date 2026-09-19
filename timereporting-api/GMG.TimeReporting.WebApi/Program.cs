﻿using GMG.TimeReporting.Core.PasswordArchiveData;
using GMG.TimeReporting.Core.TimeReportingData;
using GMG.TimeReporting.WebApi.Controllers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using System.Data.Common;
using System.Text;

const string AllowOrigins = "AllowOrigins";

// A bootstrap logger, replaced below by the one built from configuration. Without it,
// anything that goes wrong before the host is built -- a missing Jwt:Key, a malformed
// appsettings.json -- would be written nowhere and the process would simply exit.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Reads the "Serilog" section of appsettings.json, so sinks and levels are changed on the
    // server without a rebuild. ReadFrom.Services picks up enrichers registered in DI.
    builder.Services.AddSerilog((services, configuration) => configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services));

    var jwtKey = builder.Configuration["Jwt:Key"]
        ?? throw new InvalidOperationException("Missing configuration value 'Jwt:Key'.");
    var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

    builder.Services.AddCors(options =>
    {
        options.AddPolicy(AllowOrigins, policy =>
        {
            policy
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });

    builder.Services.AddDbContext<TimeReportingContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
    builder.Services.AddDbContext<PasswordArchiveContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("PasswordArchiveConnection")));

    builder.Services
        .AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;

            // Off by default this would be true, which renames inbound "role" and "sub" to the
            // long WS-Federation claim types. The RoleClaimType below would then be looking for
            // a claim name that no longer exists, and every [Authorize(Roles = ...)] check would
            // quietly 403 despite a token that plainly carries the role. Keeping the claims under
            // the names the token actually uses is what makes the two agree.
            options.MapInboundClaims = false;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,
                ValidateIssuer = false,
                ValidateAudience = false,
                NameClaimType = "username",
                RoleClaimType = "role"
            };
        });
    builder.Services.AddAuthorization();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "GMG.TimeReporting.WebApi", Version = "v1" });

        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Paste the raw token, without the \"Bearer \" prefix.",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        });

        c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            { new OpenApiSecuritySchemeReference("Bearer", document), new List<string>() }
        });
    });

    var app = builder.Build();

    await MigrateTimeReportingDatabaseAsync(app);

    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }

    // One summary line per request instead of the handful ASP.NET Core logs by default.
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            // Who made the call, so a log line can be tied back to a user. Null until
            // authentication has run, which is the case for anonymous and failed requests.
            diagnosticContext.Set("User", httpContext.User.Identity?.Name);
        };
    });

    app.UseSwagger();
    // Relative endpoint so the UI resolves correctly both at the site root (dotnet run)
    // and when the API is hosted as an IIS sub-application under /api.
    app.UseSwaggerUI(c => c.SwaggerEndpoint("v1/swagger.json", "GMG.TimeReporting.WebApi v1"));

    app.UseHttpsRedirection();
    app.UseRouting();

    // CORS must run before authentication so that unauthenticated preflight
    // requests from the UI still receive the Access-Control-Allow-* headers.
    app.UseCors(AllowOrigins);

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // HostAbortedException is how `dotnet ef` stops the host after building it to read the
    // DbContext; it is not a failure and must not be logged as one.
    Log.Fatal(ex, "The Time Reporting API terminated unexpectedly.");
    throw;
}
finally
{
    // The file sink writes on a background timer, so the last lines -- including the fatal
    // one above -- are still buffered when the process is on its way out.
    Log.CloseAndFlush();
}

/// <summary>
/// Creates the Time Reporting database at startup if the PostgreSQL server does not have it
/// yet, and brings its schema up to the latest migration.
/// </summary>
/// <remarks>
/// Only the Time Reporting database is provisioned here. The password archive is a SQL
/// Server database owned by the separate security application, which is the only thing that
/// may create it or change its shape; this API only ever reads from it. Do not add an
/// EnsureCreated or Migrate call for <see cref="PasswordArchiveContext"/> — either one would
/// let this API define a schema that is not its to define.
///
/// The PostgreSQL login role needs the CREATEDB privilege the first time this runs;
/// afterwards it only needs access to the database it created. The call is a no-op once the
/// database is in place, so a normal restart costs a couple of round trips.
/// </remarks>
static async Task MigrateTimeReportingDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    LogConnectionTargets(scope.ServiceProvider.GetRequiredService<IConfiguration>(), logger);

    var timeReporting = scope.ServiceProvider.GetRequiredService<TimeReportingContext>();
    var pending = await timeReporting.Database.GetPendingMigrationsAsync();
    if (pending.Any())
    {
        logger.LogInformation(
            "Applying {Count} Time Reporting migration(s): {Migrations}.",
            pending.Count(),
            string.Join(", ", pending));
    }

    // Creates the database first if it is missing, then applies whatever is outstanding.
    await timeReporting.Database.MigrateAsync();

    // The password archive belongs to the security application. This app only reads it, so it
    // is never created, migrated or written to — it is simply expected to be there. Checking
    // now turns a misconfigured connection string into a startup warning instead of a puzzling
    // failure at someone's first login attempt.
    var passwordArchive = scope.ServiceProvider.GetRequiredService<PasswordArchiveContext>();
    if (!await passwordArchive.Database.CanConnectAsync())
    {
        logger.LogWarning(
            "Cannot reach the password archive database. Logins will fail until "
            + "ConnectionStrings:PasswordArchiveConnection points at the security "
            + "application's database. This app never creates it.");
    }
}


/// <summary>
/// Writes both connection strings to the log, with credentials masked, as the application
/// resolved them.
/// </summary>
/// <remarks>
/// Configuration arrives from several places at once -- appsettings.json, an
/// appsettings.&lt;Environment&gt;.json beside it, app pool environment variables -- and the
/// file in source control is the one least likely to be winning. Working out which layer is in
/// effect from a connection failure alone is guesswork; the Application Name in each string
/// says which file it came from.
/// </remarks>
static void LogConnectionTargets(IConfiguration configuration, ILogger<Program> logger)
{
    logger.LogInformation(
        "Time Reporting connection: {Connection}",
        MaskCredentials(configuration.GetConnectionString("DefaultConnection")));

    logger.LogInformation(
        "Password archive connection: {Connection}",
        MaskCredentials(configuration.GetConnectionString("PasswordArchiveConnection")));
}

/// <summary>
/// Masks the username and password in a connection string, so that the logs folder -- which
/// keeps a month of these on the deployment share -- is not a place to read credentials from.
/// </summary>
static string MaskCredentials(string? connectionString)
{
    if (connectionString is null)
    {
        return "(not configured)";
    }

    DbConnectionStringBuilder builder;
    try
    {
        builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
    }
    catch (ArgumentException)
    {
        // Nothing can be masked in a string that will not parse, so none of it can be shown.
        return "(unparseable)";
    }

    // The builder lowercases keys, and both providers accept several spellings of each.
    foreach (var key in builder.Keys.Cast<string>().ToList())
    {
        var isCredential = key is "password" or "pwd" or "psw"
            or "username" or "user name" or "userid" or "user id" or "uid" or "user";

        // Empty values are left as they are: masking one would imply a credential that is not
        // actually set, which is misleading where integrated security is expected.
        if (isCredential && !string.IsNullOrEmpty(builder[key] as string))
        {
            builder[key] = "***";
        }
    }

    return builder.ConnectionString;
}
