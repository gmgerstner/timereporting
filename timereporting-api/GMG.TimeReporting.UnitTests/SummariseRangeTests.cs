using GMG.TimeReporting.Core.TimeReportingData;
using NUnit.Framework;

namespace GMG.TimeReporting.UnitTests
{
    /// <summary>
    /// Pins the bucketing rules behind the dashboard's date-range queries. No database: the
    /// whole point of splitting <see cref="TimeReportingContext.SummariseRange"/> out of
    /// <see cref="TimeReportingContext.GetRangeTimesheetAsync"/> is that these rules can be
    /// exercised directly.
    /// </summary>
    public class SummariseRangeTests
    {
        private const int Alice = 1;
        private const int Bob = 2;

        private static readonly DateTime Monday = new(2026, 9, 14);
        private static readonly DateTime Tuesday = Monday.AddDays(1);

        private static TimeEntry Entry(int id, int userId, string title, DateTime start, DateTime? end) =>
            new() { TimeEntryId = id, UserId = userId, Title = title, StartTime = start, EndTime = end };

        private static decimal? HoursFor(
            IReadOnlyList<TimeReportingContext.UserDaySummary> days,
            int userId,
            DateTime date,
            string title) =>
            days.Single(day => day.UserId == userId && day.Date == date)
                .Entries.Single(entry => entry.Title == title)
                .TotalHours;

        [Test]
        public void ChainsAnOpenEntryWithinTheSameDay()
        {
            // The positive control for the two tests below: inside one person's day, an open
            // entry is still closed off by the next one.
            var days = TimeReportingContext.SummariseRange(
            [
                Entry(1, Alice, "Open", Monday.AddHours(9), null),
                Entry(2, Alice, "Next", Monday.AddHours(11), Monday.AddHours(12))
            ]);

            Assert.That(HoursFor(days, Alice, Monday, "Open"), Is.EqualTo(2m));
        }

        [Test]
        public void DoesNotChainAcrossDays()
        {
            // Monday's open entry must not be closed by Tuesday morning, which would book it
            // as 22 hours.
            var days = TimeReportingContext.SummariseRange(
            [
                Entry(1, Alice, "Worked", Monday.AddHours(9), Monday.AddHours(10)),
                Entry(2, Alice, "Open", Monday.AddHours(11), null),
                Entry(3, Alice, "Tuesday work", Tuesday.AddHours(9), Tuesday.AddHours(10))
            ]);

            Assert.That(days, Has.Count.EqualTo(2));
            Assert.That(HoursFor(days, Alice, Monday, "Open"), Is.Null);
            Assert.That(HoursFor(days, Alice, Tuesday, "Tuesday work"), Is.EqualTo(1m));
        }

        [Test]
        public void DoesNotChainAcrossUsers()
        {
            // The same rule MultiUserTests pins for a single day, held across a range.
            var days = TimeReportingContext.SummariseRange(
            [
                Entry(1, Alice, "Alice open", Monday.AddHours(11), null),
                Entry(2, Bob, "Bob later", Monday.AddHours(15), Monday.AddHours(16))
            ]);

            Assert.That(HoursFor(days, Alice, Monday, "Alice open"), Is.Null);
            Assert.That(HoursFor(days, Bob, Monday, "Bob later"), Is.EqualTo(1m));
        }

        [Test]
        public void CountsAnEntryOnExactlyOneDay()
        {
            // GetDailyTimesheetAsync's window is inclusive at both ends, so a day and the one
            // after it both claim midnight. Bucketing a range that way would double-count
            // every boundary entry.
            var days = TimeReportingContext.SummariseRange(
            [
                Entry(1, Alice, "Late", Monday.AddHours(23), Monday.AddHours(23).AddMinutes(30)),
                Entry(2, Alice, "Early", Tuesday.AddMinutes(15), Tuesday.AddHours(1))
            ]);

            Assert.That(days, Has.Count.EqualTo(2));
            Assert.That(HoursFor(days, Alice, Monday, "Late"), Is.EqualTo(0.5m));
            Assert.That(HoursFor(days, Alice, Tuesday, "Early"), Is.EqualTo(0.75m));
            Assert.That(
                days.Single(day => day.Date == Monday).Entries.Select(entry => entry.Title),
                Is.EquivalentTo(new[] { "Late" }));
        }

        [Test]
        public void ReturnsNothingForNoEntries()
        {
            Assert.That(TimeReportingContext.SummariseRange([]), Is.Empty);
        }
    }
}
