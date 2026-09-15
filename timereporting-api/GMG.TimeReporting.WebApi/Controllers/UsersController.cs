using GMG.TimeReporting.Core.TimeReportingData;
using GMG.TimeReporting.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GMG.TimeReporting.WebApi.Controllers
{
    /// <summary>
    /// Lets an admin find out who they can look at. Admin-only in full: a regular user has
    /// no reason to enumerate everyone else.
    /// </summary>
    [Authorize(Roles = ClaimsPrincipalExtensions.AdminRole)]
    [Route("[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly TimeReportingContext context;

        public UsersController(TimeReportingContext context)
        {
            this.context = context;
        }

        /// <summary>
        /// Everyone who has logged in at least once, for the admin's user picker.
        /// </summary>
        [HttpGet]
        [Route("GetUsers")]
        public async Task<IEnumerable<UserSummary>> GetUsers(CancellationToken cancellationToken)
        {
            return await context.Users
                .AsNoTracking()
                .OrderBy(u => u.Username)
                .Select(u => new UserSummary
                {
                    UserId = u.UserId,
                    Username = u.Username,
                    IsAdmin = u.IsAdmin
                })
                .ToListAsync(cancellationToken);
        }
    }
}
