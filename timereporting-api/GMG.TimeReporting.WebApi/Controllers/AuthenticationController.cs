using GMG.TimeReporting.Core.PasswordArchiveData;
using GMG.TimeReporting.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace GMG.TimeReporting.WebApi.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        private const int DefaultTokenExpirationMinutes = 240;
        private const string TimeReportingUrl = "https://timereporting.gmgdesk.com";

        private readonly IConfiguration configuration;
        private readonly PasswordArchiveContext passwordArchiveContext;
        private readonly ILogger<AuthenticationController> logger;

        public AuthenticationController(
            IConfiguration configuration,
            PasswordArchiveContext passwordArchiveContext,
            ILogger<AuthenticationController> logger)
        {
            this.configuration = configuration;
            this.passwordArchiveContext = passwordArchiveContext;
            this.logger = logger;
        }

        /// <summary>
        /// Log in to the system and receive a bearer token.
        /// </summary>
        [Route("Login")]
        [AllowAnonymous]
        [HttpPost]
        public async Task<ActionResult<User>> Login([FromBody] LoginCredentials login, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Attempting login for user: {login.Username}");
            //#if DEBUG
            var owner = new Password
            {
                PasswordId = 1,
                SystemUserId = 1,
                Username = login.Username,
                PasswordValue = login.Password
            };
            // #else
            // var owner = await passwordArchiveContext.Passwords
            //     .Where(p => p.Url == TimeReportingUrl)
            //     .Where(p => p.Username == login.Username)
            //     .SingleOrDefaultAsync(cancellationToken);

            // if (owner is null || owner.PasswordValue != login.Password)
            // {
            //     logger.LogWarning("Invalid log in for user: {Username}", login.Username);
            //     return BadRequest();
            // }
            // #endif
            Console.WriteLine($"User {login.Username} logged in successfully.");


            var expires = DateTime.Now.AddMinutes(DefaultTokenExpirationMinutes);

            var endOfWorkDay = DateTime.Today.AddHours(17); // 5 PM
            if (endOfWorkDay > expires)
            {
                // Prevent expiration before the work day is done.
                expires = endOfWorkDay;
            }

            var result = new User
            {
                Username = owner.Username ?? login.Username,
                Token = GenerateJsonWebToken(owner, expires),
                Expires = expires
            };

            logger.LogInformation("User logged in: {Username}", login.Username);
            return Ok(result);
        }

        private string GenerateJsonWebToken(Password userInfo, DateTime expires)
        {
            var jwtKey = configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("Missing configuration value 'Jwt:Key'.");
            var issuer = configuration["Jwt:Issuer"];

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = issuer,
                Audience = issuer,
                Expires = expires.ToUniversalTime(),
                SigningCredentials = credentials,
                Claims = new Dictionary<string, object>
                {
                    [JwtRegisteredClaimNames.Sub] = userInfo.SystemUserId.ToString(),
                    [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
                    ["userid"] = userInfo.PasswordId.ToString(),
                    ["username"] = userInfo.Username ?? string.Empty
                }
            };

            return new JsonWebTokenHandler().CreateToken(descriptor);
        }
    }
}
