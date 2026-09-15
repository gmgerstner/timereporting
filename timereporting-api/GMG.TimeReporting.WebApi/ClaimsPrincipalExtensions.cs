using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;

namespace GMG.TimeReporting.WebApi
{
    /// <summary>
    /// Reads the Time Reporting claims out of a validated bearer token.
    /// </summary>
    public static class ClaimsPrincipalExtensions
    {
        public const string AdminRole = "Admin";

        /// <summary>
        /// The signed-in user's local <c>Users.UserId</c>.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The token carried no usable subject. Endpoints that call this are all
        /// <c>[Authorize]</c>d, so a token that got this far but has no subject is a bug in
        /// token generation rather than something a caller can trigger.
        /// </exception>
        public static int GetUserId(this ClaimsPrincipal principal)
        {
            var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

            return int.TryParse(value, out var userId)
                ? userId
                : throw new InvalidOperationException("The bearer token has no usable subject claim.");
        }

        public static bool IsAdmin(this ClaimsPrincipal principal) =>
            principal.IsInRole(AdminRole);
    }
}
