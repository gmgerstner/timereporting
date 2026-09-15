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
