using GMG.TimeReporting.Core.PasswordArchiveData;
using GMG.TimeReporting.Core.TimeReportingData;
using GMG.TimeReporting.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;
using CoreUser = GMG.TimeReporting.Core.TimeReportingData.User;

namespace GMG.TimeReporting.WebApi.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        private const int DefaultTokenExpirationMinutes = 240;

        /// <summary>
        /// The archive stores one password row per site; this is the one this app owns.
        /// </summary>
        public const string TimeReportingUrl = "https://timereporting.gmgdesk.com";

        private readonly IConfiguration configuration;
        private readonly PasswordArchiveContext passwordArchiveContext;
        private readonly TimeReportingContext timeReportingContext;
        private readonly ILogger<AuthenticationController> logger;

        public AuthenticationController(
            IConfiguration configuration,
            PasswordArchiveContext passwordArchiveContext,
            TimeReportingContext timeReportingContext,
            ILogger<AuthenticationController> logger)
        {
            this.configuration = configuration;
            this.passwordArchiveContext = passwordArchiveContext;
            this.timeReportingContext = timeReportingContext;
            this.logger = logger;
        }

        /// <summary>
        /// Log in to the system and receive a bearer token.
        /// </summary>
        [Route("Login")]
        [AllowAnonymous]
        [HttpPost]
        public async Task<ActionResult<Models.User>> Login([FromBody] LoginCredentials login, CancellationToken cancellationToken)
        {
            // Usernames are matched case-insensitively. PostgreSQL compares text
            // case-sensitively, so without this "GMG" and "gmg" would be different people.
            var username = (login.Username ?? string.Empty).Trim().ToLowerInvariant();
            if (username.Length == 0)
            {
                return BadRequest();
            }

//#if !DEBUG
            Password? owner = await passwordArchiveContext.Passwords
                .AsNoTracking()
                .Where(p => p.Url == TimeReportingUrl)
                .Where(p => p.Username!.ToLower() == username)
                .SingleOrDefaultAsync(cancellationToken);
//#else
//            // This is a temporary bypass for development. It allows logging in with any username
//            // and password, which is convenient for testing. It should be removed before production.
//            if (login.Password != "george")
//            {
//                return BadRequest();
//            }
//            Password? owner = new Password
//            {
//                Url = TimeReportingUrl,
//                Username = username,
//                PasswordValue = "dev"
//            };
//#endif


            if (owner is null || !PasswordMatches(owner.PasswordValue, login.Password))
            {
                // Same response either way, so the endpoint does not reveal which usernames exist.
                logger.LogWarning("Invalid log in for user: {Username}", username);
                return BadRequest();
            }

            var user = await ResolveUserAsync(username, cancellationToken);

            var expires = DateTime.Now.AddMinutes(DefaultTokenExpirationMinutes);

            var endOfWorkDay = DateTime.Today.AddHours(17); // 5 PM
            if (endOfWorkDay > expires)
            {
                // Prevent expiration before the work day is done.
                expires = endOfWorkDay;
            }

            var result = new Models.User
            {
                Username = user.Username,
                IsAdmin = user.IsAdmin,
                Token = GenerateJsonWebToken(user, expires),
                Expires = expires
            };

            logger.LogInformation("User logged in: {Username} (id {UserId})", user.Username, user.UserId);
            return Ok(result);
        }

        /// <summary>
        /// Finds this user's local row, creating it the first time they log in.
        /// </summary>
        /// <remarks>
        /// New users are never admins. Promote one with
        /// <c>UPDATE "Users" SET "IsAdmin" = true WHERE "Username" = '...'</c>.
        /// </remarks>
        private async Task<CoreUser> ResolveUserAsync(string username, CancellationToken cancellationToken)
        {
            var existing = await timeReportingContext.Users
                .SingleOrDefaultAsync(u => u.Username == username, cancellationToken);
            if (existing is not null)
            {
                return existing;
            }

            var created = new CoreUser
            {
                Username = username,
                IsAdmin = false,
                CreatedDate = DateTime.Now
            };
            timeReportingContext.Users.Add(created);

            try
            {
                await timeReportingContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Two first logins at once: the unique index on Username rejected the loser,
                // so take the row the winner inserted.
                timeReportingContext.Entry(created).State = EntityState.Detached;
                return await timeReportingContext.Users
                    .SingleAsync(u => u.Username == username, cancellationToken);
            }

            logger.LogInformation("Created a local user for {Username}.", username);
            return created;
        }

        /// <summary>
        /// Compares the supplied password against the archive in constant time.
        /// </summary>
        /// <remarks>
        /// The archive holds passwords in plain text — that is the security application's
        /// schema, not ours. This at least stops the comparison itself from leaking the
        /// password a character at a time through its timing.
        /// </remarks>
        private static bool PasswordMatches(string? stored, string? supplied) =>
            CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(stored ?? string.Empty),
                Encoding.UTF8.GetBytes(supplied ?? string.Empty));

        private string GenerateJsonWebToken(CoreUser user, DateTime expires)
        {
            var jwtKey = configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("Missing configuration value 'Jwt:Key'.");
            var issuer = configuration["Jwt:Issuer"];

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.UserId.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
                ["userid"] = user.UserId.ToString(),
                ["username"] = user.Username
            };

            if (user.IsAdmin)
            {
                claims["role"] = "Admin";
            }

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = issuer,
                Audience = issuer,
                Expires = expires.ToUniversalTime(),
                SigningCredentials = credentials,
                Claims = claims
            };

            return new JsonWebTokenHandler().CreateToken(descriptor);
        }
    }
}
