using GMG.TimeReporting.Core.TimeReportingData;
using GMG.TimeReporting.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GMG.TimeReporting.WebApi.Controllers
{
    /// <summary>
    /// Cross-user reporting. Admin-only in full: these are the only endpoints that read
    /// everyone's time at once, which no regular user has a reason to do.
    /// </summary>
    [Authorize(Roles = ClaimsPrincipalExtensions.AdminRole)]
    [Route("[controller]")]
    [ApiController]
    public class ReportsController : ControllerBase
    {
        private readonly TimeReportingContext context;

        public ReportsController(TimeReportingContext context)
        {
            this.context = context;
        }

        /// <summary>
        /// Hours per person over a date range, highest first.
        /// </summary>
        /// <remarks>
        /// Everyone with an account is listed, including people who logged nothing in the
        /// range — an empty row is the point of the report as often as a full one is.
        /// </remarks>
        [HttpGet]
        [Route("GetHoursByUser")]
        public async Task<ActionResult<IEnumerable<UserHours>>> GetHoursByUser(DateTime? From, DateTime? To, CancellationToken cancellationToken)
        {
            if (!DateRange.TryResolve(From, To, out var range, out var error))
            {
                return BadRequest(error);
            }

            var users = await context.Users
                .AsNoTracking()
                .Select(user => new { user.UserId, user.Username })
                .ToListAsync(cancellationToken);

            var days = await context.GetRangeTimesheetAsync(null, range.From, range.To, cancellationToken);

            var byUser = days
                .GroupBy(day => day.UserId)
                .ToDictionary(
                    group => group.Key,
                    group => new
                    {
                        TotalHours = group.Sum(day => day.Entries.Sum(entry => entry.TotalHours ?? 0m)),
                        DaysLogged = group.Count()
                    });

            return users
                .Select(user =>
                {
                    var totals = byUser.GetValueOrDefault(user.UserId);
                    return new UserHours
                    {
                        UserId = user.UserId,
                        Username = user.Username,
                        TotalHours = totals?.TotalHours ?? 0m,
                        DaysLogged = totals?.DaysLogged ?? 0
                    };
                })
                .OrderByDescending(user => user.TotalHours)
                .ThenBy(user => user.Username, StringComparer.Ordinal)
                .ToList();
        }
    }
}
