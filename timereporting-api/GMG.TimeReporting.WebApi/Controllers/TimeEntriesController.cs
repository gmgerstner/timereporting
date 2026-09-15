using GMG.TimeReporting.Core.TimeReportingData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GMG.TimeReporting.WebApi.Controllers
{
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

        [HttpPost]
        [Route("StartClock")]
        public async Task<int> StartClock(string Title, DateTime Time, CancellationToken cancellationToken)
        {
            var data = new TimeEntry
            {
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
            var data = await context.TimeEntries
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
            var data = await context.TimeEntries
                .FirstOrDefaultAsync(te => te.TimeEntryId == id, cancellationToken);
            if (data is null) return NotFound();

            return data;
        }

        [HttpPost]
        [Route("EditTimeEntry")]
        public async Task<IActionResult> EditTimeEntry(int id, string Title, DateTime StartTime, DateTime? EndTime, CancellationToken cancellationToken)
        {
            var data = await context.TimeEntries
                .FirstOrDefaultAsync(te => te.TimeEntryId == id, cancellationToken);
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
            var item = await context.TimeEntries
                .SingleOrDefaultAsync(te => te.TimeEntryId == id, cancellationToken);
            if (item is null) return NotFound();

            context.TimeEntries.Remove(item);
            await context.SaveChangesAsync(cancellationToken);
            return Ok();
        }

        [HttpGet]
        [Route("GetSchedule")]
        public async Task<IEnumerable<TimeEntry>> GetSchedule(DateTime? WorkDate, CancellationToken cancellationToken)
        {
            var workDate = (WorkDate ?? DateTime.Today).Date;

            return await context.TimeEntries
                .Where(te => te.StartTime >= workDate)
                .OrderBy(te => te.StartTime)
                .ToListAsync(cancellationToken);
        }

        [HttpGet]
        [Route("GetDailyTimeSheet")]
        public async Task<IEnumerable<TimeReportingContext.TimeSheetEntry>> GetDailyTimeSheet(DateTime? WorkDate, CancellationToken cancellationToken)
        {
            return await context.GetDailyTimesheetAsync(WorkDate, cancellationToken);
        }

        [HttpGet]
        [Route("GetRecentTitles")]
        public async Task<IEnumerable<string>> GetRecentTitles(CancellationToken cancellationToken)
        {
            var cutOff = DateTime.Today.AddDays(-21);

            var favorites = await context.CommonTasks
                .Select(r => r.Title)
                .ToListAsync(cancellationToken);

            var list = await context.TimeEntries
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
            return await context.CommonTasks
                .Select(r => r.Title)
                .OrderBy(r => r)
                .ToListAsync(cancellationToken);
        }
    }
}
