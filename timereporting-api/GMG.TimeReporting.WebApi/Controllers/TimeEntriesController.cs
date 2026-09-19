using GMG.TimeReporting.Core.TimeReportingData;
using GMG.TimeReporting.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GMG.TimeReporting.WebApi.Controllers
{
    /// <summary>
    /// Every endpoint here works on the signed-in user's own entries.
    /// </summary>
    /// <remarks>
    /// The two read endpoints — <see cref="GetSchedule"/> and <see cref="GetDailyTimeSheet"/> —
    /// take an optional <c>UserId</c> so an admin can look at someone else's day. Nothing
    /// here lets anyone write to another user's entries, admin or not: the write endpoints
    /// resolve the row by id *and* user, so another user's id simply does not match.
    /// </remarks>
    [Authorize]
    [Route("[controller]")]
    [ApiController]
    public class TimeEntriesController : ControllerBase
    {
        private readonly TimeReportingContext context;

        public TimeEntriesController(TimeReportingContext context)
        {
            this.context = context;
        }

        private int CurrentUserId => User.GetUserId();

        /// <summary>
        /// Resolves which user a read endpoint should report on.
        /// </summary>
        /// <returns>
        /// The user id to query, or <c>null</c> when the caller asked for someone else's data
        /// without being an admin.
        /// </returns>
        private int? ResolveReadTarget(int? requestedUserId)
        {
            var currentUserId = CurrentUserId;
            if (requestedUserId is null || requestedUserId == currentUserId)
            {
                return currentUserId;
            }

            return User.IsAdmin() ? requestedUserId : null;
        }

        [HttpPost]
        [Route("StartClock")]
        public async Task<int> StartClock(string Title, DateTime Time, CancellationToken cancellationToken)
        {
            var data = new TimeEntry
            {
                UserId = CurrentUserId,
                Title = Title,
                StartTime = Time,
                EndTime = null
            };
            context.TimeEntries.Add(data);
            await context.SaveChangesAsync(cancellationToken);
            return data.TimeEntryId;
        }

        [HttpPost]
        [Route("StopClock")]
        public async Task<IActionResult> StopClock(DateTime Time, CancellationToken cancellationToken)
        {
            var userId = CurrentUserId;

            var data = await context.TimeEntries
                .Where(te => te.UserId == userId)
                .Where(te => te.StartTime >= DateTime.Today)
                .OrderByDescending(te => te.StartTime)
                .FirstOrDefaultAsync(cancellationToken);
            if (data is null) return NotFound();

            data.EndTime = Time;
            await context.SaveChangesAsync(cancellationToken);
            return Ok();
        }

        [HttpGet]
        [Route("GetTimeEntry")]
        public async Task<ActionResult<TimeEntry>> GetTimeEntry(int id, CancellationToken cancellationToken)
        {
            var userId = CurrentUserId;

            var data = await context.TimeEntries
                .FirstOrDefaultAsync(te => te.TimeEntryId == id && te.UserId == userId, cancellationToken);
            if (data is null) return NotFound();

            return data;
        }

        [HttpPost]
        [Route("EditTimeEntry")]
        public async Task<IActionResult> EditTimeEntry(int id, string Title, DateTime StartTime, DateTime? EndTime, CancellationToken cancellationToken)
        {
            var userId = CurrentUserId;

            var data = await context.TimeEntries
                .FirstOrDefaultAsync(te => te.TimeEntryId == id && te.UserId == userId, cancellationToken);
            if (data is null) return NotFound();

            data.Title = Title;
            data.StartTime = StartTime;
            data.EndTime = EndTime;
            await context.SaveChangesAsync(cancellationToken);
            return Ok();
        }

        [HttpDelete]
        [Route("DeleteTimeEntry")]
        public async Task<IActionResult> DeleteTimeEntry(int id, CancellationToken cancellationToken)
        {
            var userId = CurrentUserId;

            var item = await context.TimeEntries
                .SingleOrDefaultAsync(te => te.TimeEntryId == id && te.UserId == userId, cancellationToken);
            if (item is null) return NotFound();

            context.TimeEntries.Remove(item);
            await context.SaveChangesAsync(cancellationToken);
            return Ok();
        }

        /// <summary>
        /// The signed-in user's entries from <paramref name="WorkDate"/> onwards, or another
        /// user's when an admin passes <paramref name="UserId"/>.
        /// </summary>
        [HttpGet]
        [Route("GetSchedule")]
        public async Task<ActionResult<IEnumerable<TimeEntry>>> GetSchedule(DateTime? WorkDate, int? UserId, CancellationToken cancellationToken)
        {
            if (ResolveReadTarget(UserId) is not int userId) return Forbid();

            var workDate = (WorkDate ?? DateTime.Today).Date;

            return await context.TimeEntries
                .Where(te => te.UserId == userId)
                .Where(te => te.StartTime >= workDate)
                .OrderBy(te => te.StartTime)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Hours per title for one day, for the signed-in user or — for an admin passing
        /// <paramref name="UserId"/> — for someone else.
        /// </summary>
        [HttpGet]
        [Route("GetDailyTimeSheet")]
        public async Task<ActionResult<IEnumerable<TimeReportingContext.TimeSheetEntry>>> GetDailyTimeSheet(DateTime? WorkDate, int? UserId, CancellationToken cancellationToken)
        {
            if (ResolveReadTarget(UserId) is not int userId) return Forbid();

            var sheet = await context.GetDailyTimesheetAsync(userId, WorkDate, cancellationToken);
            return sheet.ToList();
        }

        /// <summary>
        /// Everything the dashboard plots over a date range, for the signed-in user or — for
        /// an admin passing <paramref name="UserId"/> — for someone else.
        /// </summary>
        /// <remarks>
        /// Both breakdowns come back together because they are two readings of a single scan
        /// of the same rows, shown side by side against one date range.
        /// </remarks>
        [HttpGet]
        [Route("GetRangeSummary")]
        public async Task<ActionResult<RangeSummary>> GetRangeSummary(DateTime? From, DateTime? To, int? UserId, CancellationToken cancellationToken)
        {
            if (ResolveReadTarget(UserId) is not int userId) return Forbid();

            if (!DateRange.TryResolve(From, To, out var range, out var error))
            {
                return BadRequest(error);
            }

            var days = await context.GetRangeTimesheetAsync(userId, range.From, range.To, cancellationToken);

            // A day that is present but still running sums to zero, which is the same number
            // a day with no entries at all shows. Only the presence of a row counts as logged.
            var hoursByDay = days.ToDictionary(
                day => day.Date,
                day => day.Entries.Sum(entry => entry.TotalHours ?? 0m));

            var titleTotals = days
                .SelectMany(day => day.Entries)
                .GroupBy(entry => entry.Title, StringComparer.Ordinal)
                .Select(group => new TitleTotal
                {
                    Title = group.Key,
                    TotalHours = group.Sum(entry => entry.TotalHours ?? 0m)
                })
                .OrderByDescending(total => total.TotalHours)
                .ThenBy(total => total.Title, StringComparer.Ordinal)
                .ToList();

            return new RangeSummary
            {
                TotalHours = hoursByDay.Values.Sum(),
                DaysLogged = days.Count,
                DailyTotals = range.EnumerateDays()
                    .Select(day => new DailyTotal
                    {
                        Date = day,
                        TotalHours = hoursByDay.GetValueOrDefault(day)
                    })
                    .ToList(),
                TitleTotals = titleTotals
            };
        }

        [HttpGet]
        [Route("GetRecentTitles")]
        public async Task<IEnumerable<string>> GetRecentTitles(CancellationToken cancellationToken)
        {
            var userId = CurrentUserId;
            var cutOff = DateTime.Today.AddDays(-21);

            var favorites = await context.CommonTasks
                .Where(r => r.UserId == userId)
                .Select(r => r.Title)
                .ToListAsync(cancellationToken);

            var list = await context.TimeEntries
                .Where(te => te.UserId == userId)
                .Where(te => te.StartTime >= cutOff)
                .Where(te => !favorites.Contains(te.Title))
                .GroupBy(te => te.Title)
                .Select(g => new
                {
                    Title = g.Key,
                    LastStartTime = g.Max(te => te.StartTime)
                })
                .ToListAsync(cancellationToken);

            return list
                .OrderByDescending(te => te.LastStartTime)
                .ThenBy(te => te.Title)
                .Select(te => te.Title)
                .ToList();
        }

        [HttpGet]
        [Route("GetCommonTitles")]
        public async Task<IEnumerable<string>> GetCommonTitles(CancellationToken cancellationToken)
        {
            var userId = CurrentUserId;

            return await context.CommonTasks
                .Where(r => r.UserId == userId)
                .Select(r => r.Title)
                .OrderBy(r => r)
                .ToListAsync(cancellationToken);
        }
    }
}
