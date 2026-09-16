using Microsoft.EntityFrameworkCore;

namespace GMG.TimeReporting.Core.TimeReportingData
{
    /// <summary>
    /// C# ports of the two stored procedures that used to live in the
    /// GMG.TimeReporting.Database SSDT project (dbo.GetDailyTimesheet and
    /// dbo.GetRecentTasks). They are kept here so the logic survives the move to
    /// code-first; the database no longer needs either procedure.
    /// </summary>
    public partial class TimeReportingContext : DbContext
    {
        /// <summary>
        /// The favourite task titles that the legacy dbo.GetRecentTasks procedure carried
        /// as an inline XML literal.
        /// </summary>
        public static readonly IReadOnlyList<string> LegacyFavoriteTitles =
        [
            "Team meeting",
            "Admin",
            "Daily Scrum",
            "Qualtrax"
        ];

        /// <summary>
        /// Hours booked per task title for a single work day, for one user.
        /// </summary>
        /// <remarks>
        /// Port of dbo.GetDailyTimesheet. An entry that has no EndTime is treated as ending
        /// when the next entry of the same day starts; the last entry of the day, if it is
        /// still running, contributes no hours (the original returned NULL for it).
        /// <para>
        /// The original procedure predates multiple users and summed across the whole table.
        /// Chaining an open entry onto the next one only makes sense within a single person's
        /// day, so the filter is applied before the entries are paired up.
        /// </para>
        /// </remarks>
        public async Task<IReadOnlyList<TimeSheetEntry>> GetDailyTimesheetAsync(
            int userId,
            DateTime? date,
            CancellationToken cancellationToken = default)
        {
            var workDate = date ?? DateTime.Today;

            // The original used BETWEEN @WorkDate AND DATEADD(DAY, 1, @WorkDate), which is
            // inclusive at both ends. Preserved so per-day totals stay identical.
            var dayAfter = workDate.AddDays(1);

            var entries = await TimeEntries
                .AsNoTracking()
                .Where(te => te.UserId == userId)
                .Where(te => te.StartTime >= workDate && te.StartTime <= dayAfter)
                .OrderBy(te => te.StartTime)
                .ThenBy(te => te.TimeEntryId)
                .ToListAsync(cancellationToken);

            return SummariseTimesheet(entries);
        }

        /// <summary>
        /// The in-memory half of <see cref="GetDailyTimesheetAsync"/>, separated so the
        /// chaining and rounding rules can be exercised without a database.
        /// </summary>
        /// <param name="entries">One day's entries, ordered by start time.</param>
        public static IReadOnlyList<TimeSheetEntry> SummariseTimesheet(IReadOnlyList<TimeEntry> entries)
        {
            var totals = new Dictionary<string, decimal?>(StringComparer.Ordinal);

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];

                // ISNULL(te1.EndTime, te2.StartTime): fall back to the next entry's start.
                var end = entry.EndTime ?? (i + 1 < entries.Count ? entries[i + 1].StartTime : null);
                var hours = HoursBetween(entry.StartTime, end);

                if (!totals.TryGetValue(entry.Title, out var running))
                {
                    totals[entry.Title] = hours;
                    continue;
                }

                // SUM() ignores NULLs unless every value is NULL.
                totals[entry.Title] = running is null
                    ? hours
                    : running + (hours ?? 0m);
            }

            return totals
                .Select(pair => new TimeSheetEntry { Title = pair.Key, TotalHours = pair.Value })
                .OrderBy(entry => entry.Title, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Task titles ordered by how recently they were last started, with the favourite
        /// titles pinned to the top.
        /// </summary>
        /// <remarks>
        /// Port of dbo.GetRecentTasks. Note that the API's own
        /// <c>TimeEntries/GetRecentTitles</c> endpoint does not use this: it reads favourites
        /// from the CommonTasks table and applies a 21-day cut-off instead. This method is
        /// retained so the original procedure's behaviour is not lost.
        /// </remarks>
        /// <param name="userId">The user whose entries to look at.</param>
        /// <param name="favorites">
        /// Titles to pin to the top. Defaults to <see cref="LegacyFavoriteTitles"/>.
        /// </param>
        public async Task<IReadOnlyList<RecentTask>> GetRecentTasksAsync(
            int userId,
            IEnumerable<string>? favorites = null,
            CancellationToken cancellationToken = default)
        {
            var favoriteSet = new HashSet<string>(
                favorites ?? LegacyFavoriteTitles,
                StringComparer.OrdinalIgnoreCase);

            var grouped = await TimeEntries
                .AsNoTracking()
                .Where(te => te.UserId == userId)
                .GroupBy(te => te.Title)
                .Select(g => new RecentTask
                {
                    Title = g.Key,
                    LastStarted = g.Max(te => te.StartTime)
                })
                .ToListAsync(cancellationToken);

            // UPDATE #Results SET LastStarted = GETDATE() WHERE Title IN (favorites)
            var now = DateTime.Now;
            foreach (var task in grouped.Where(task => favoriteSet.Contains(task.Title)))
            {
                task.LastStarted = now;
            }

            return grouped
                .OrderByDescending(task => task.LastStarted)
                .ThenBy(task => task.Title, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Whole hours between two instants, matching
        /// <c>DATEDIFF(MINUTE, start, end) / 60.0</c>. SQL Server counts minute boundaries
        /// crossed, which is the same as truncating both values to the minute and
        /// subtracting.
        /// </summary>
        private static decimal? HoursBetween(DateTime start, DateTime? end)
        {
            if (end is null)
            {
                return null;
            }

            var minutes = (long)(TruncateToMinute(end.Value) - TruncateToMinute(start)).TotalMinutes;
            return minutes / 60m;
        }

        private static DateTime TruncateToMinute(DateTime value) =>
            new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, value.Kind);

        public class TimeSheetEntry
        {
            public string Title { get; set; } = string.Empty;

            public decimal? TotalHours { get; set; }
        }

        public class RecentTask
        {
            public string Title { get; set; } = string.Empty;

            public DateTime LastStarted { get; set; }
        }
    }
}
