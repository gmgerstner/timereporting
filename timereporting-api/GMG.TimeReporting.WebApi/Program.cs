using GMG.TimeReporting.Core.PasswordArchiveData;
using GMG.TimeReporting.Core.TimeReportingData;
using GMG.TimeReporting.WebApi.Controllers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;

const string AllowOrigins = "AllowOrigins";

var builder = WebApplication.CreateBuilder(args);

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
    options.UseNpgsql(builder.Configuration.GetConnectionString("PasswordArchiveConnection")));

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

await ProvisionDatabasesAsync(app);

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

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

/// <summary>
/// Creates both databases at startup if the PostgreSQL server does not have them yet,
/// and brings the Time Reporting schema up to the latest migration.
/// </summary>
/// <remarks>
/// The login role needs the CREATEDB privilege the first time this runs; afterwards it
/// only needs access to the two databases. Both calls are no-ops once everything is in
/// place, so a normal restart costs a couple of round trips.
/// </remarks>
static async Task ProvisionDatabasesAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

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

    // The password archive belongs to the security application. This app only reads it, so
    // outside Development it is never created, migrated or written to — it is simply expected
    // to be there. Checking now turns a misconfigured connection string into a startup warning
    // instead of a puzzling failure at someone's first login attempt.
    var passwordArchive = scope.ServiceProvider.GetRequiredService<PasswordArchiveContext>();

    if (app.Environment.IsDevelopment())
    {
        await SeedDevelopmentArchiveAsync(passwordArchive, timeReporting, logger);
    }
    else if (!await passwordArchive.Database.CanConnectAsync())
    {
        logger.LogWarning(
            "Cannot reach the password archive database. Logins will fail until "
            + "ConnectionStrings:PasswordArchiveConnection points at the security "
            + "application's database. This app never creates it.");
    }
}

/// <summary>
/// Builds a local stand-in password archive and gives it two accounts to sign in with, so a
/// fresh clone is usable without a copy of the security application's database.
/// </summary>
/// <remarks>
/// This is the only place in the app that writes to the archive at all, and it runs in
/// Development only. It seeds credentials; it does not weaken the check on them — logging in
/// verifies the supplied password against the archive in every environment. It also stops at
/// the first sign of a real archive: an existing database is never created over, and one that
/// already holds a Time Reporting entry is left completely alone.
/// </remarks>
static async Task SeedDevelopmentArchiveAsync(
    PasswordArchiveContext passwordArchive,
    TimeReportingContext timeReporting,
    ILogger logger)
{
    const string developmentSystemUsername = "development";

    // Username and password are the same string, purely so they are easy to remember.
    // "admin" is the one that gets the role; "user" is a plain account to test isolation
    // against, and to give the admin's user picker someone to pick.
    string[] devUsernames = ["admin", "user"];

    // Creates the database only when it is missing; returns false and writes nothing when
    // the developer already has a real archive to point at.
    if (await passwordArchive.Database.EnsureCreatedAsync())
    {
        logger.LogInformation("Created a Development password archive database from the model.");
    }

    var url = AuthenticationController.TimeReportingUrl;
    var now = DateTime.Now;

    if (!await passwordArchive.Passwords.AnyAsync(p => p.Url == url))
    {
        var systemUser = new SystemUser
        {
            SystemUsername = developmentSystemUsername,
            HashedSystemPassword = new string('0', 64)
        };

        foreach (var name in devUsernames)
        {
            passwordArchive.Passwords.Add(new Password
            {
                SystemUser = systemUser,
                Title = $"Time Reporting ({name})",
                Username = name,
                PasswordValue = name,
                Url = url,
                CreatedDate = now,
                LastModifiedDate = now
            });
        }

        await passwordArchive.SaveDevelopmentSeedDataAsync();

        logger.LogWarning(
            "Seeded Development log-ins: {Usernames} (password same as username). Development only.",
            string.Join(", ", devUsernames));
    }

    // Local user rows are normally created by logging in, which means a seeded account does
    // not exist — and so cannot be picked in the admin's "Viewing" list — until someone has
    // actually signed in as it. Create them up front so a fresh clone has something to look
    // at, and so "admin" really is an admin rather than the plain account login would make.
    //
    // Only for an archive this seeder built. A real archive is owned by the security
    // application's own system users, never by a row we invented called "development", so
    // pointing a Development run at the real thing grants nobody anything here.
    var isSeededArchive = await passwordArchive.Passwords
        .AnyAsync(p => p.Url == url && p.SystemUser.SystemUsername == developmentSystemUsername);
    if (!isSeededArchive)
    {
        return;
    }

    var existing = await timeReporting.Users
        .Where(u => devUsernames.Contains(u.Username))
        .Select(u => u.Username)
        .ToListAsync();

    var missing = devUsernames.Except(existing).ToList();
    if (missing.Count == 0)
    {
        return;
    }

    foreach (var name in missing)
    {
        timeReporting.Users.Add(new User
        {
            Username = name,
            IsAdmin = name == "admin",
            CreatedDate = now
        });
    }

    await timeReporting.SaveChangesAsync();

    logger.LogInformation(
        "Created Development user rows: {Usernames}.",
        string.Join(", ", missing));
}
