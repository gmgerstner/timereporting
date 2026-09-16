using GMG.TimeReporting.Core.PasswordArchiveData;
using GMG.TimeReporting.Core.TimeReportingData;
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
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateIssuer = false,
            ValidateAudience = false
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
/// Creates the Time Reporting database at startup if the PostgreSQL server does not have it
/// yet, and brings its schema up to the latest migration.
/// </summary>
/// <remarks>
/// Only the Time Reporting database is provisioned here. The password archive is a SQL
/// Server database owned by the separate security application, which is the only thing that
/// may create it or change its shape; this API reads and writes its rows and nothing more.
/// Do not add an EnsureCreated or Migrate call for <see cref="PasswordArchiveContext"/> —
/// either one would let this API define a schema that is not its to define.
///
/// The PostgreSQL login role needs the CREATEDB privilege the first time this runs;
/// afterwards it only needs access to the database it created. The call is a no-op once the
/// database is in place, so a normal restart costs a couple of round trips.
/// </remarks>
static async Task MigrateTimeReportingDatabaseAsync(WebApplication app)
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
}
