using GMG.TimeReporting.Core.TimeReportingData;
using NUnit.Framework;

namespace GMG.TimeReporting.UnitTests
{
    /// <summary>
    /// Covers the C# port of the former dbo.GetDailyTimesheet stored procedure.
    /// </summary>
    public class TimesheetTests
    {
        private static readonly DateTime Day = new(2024, 5, 6);

        private static TimeEntry Entry(string title, string start, string? end, int id = 0) =>
            new()
            {
                TimeEntryId = id,
                Title = title,
                StartTime = Day.Add(TimeSpan.Parse(start)),
                EndTime = end is null ? null : Day.Add(TimeSpan.Parse(end))
            };

        private static IReadOnlyList<TimeReportingContext.TimeSheetEntry> Summarise(
            params TimeEntry[] entries) =>
            TimeReportingContext.SummariseTimesheet(entries);

        [Test]
        public void ExplicitEndTime_IsMeasuredDirectly()
        {
            var result = Summarise(Entry("Admin", "09:00", "10:30"));

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].Title, Is.EqualTo("Admin"));
            Assert.That(result[0].TotalHours, Is.EqualTo(1.5m));
        }

        [Test]
        public void MissingEndTime_FallsBackToNextEntryStart()
        {
            // ISNULL(te1.EndTime, te2.StartTime) in the original procedure.
            var result = Summarise(
                Entry("Admin", "09:00", null, 1),
                Entry("Standup", "09:15", "09:30", 2));

            Assert.That(result.Single(r => r.Title == "Admin").TotalHours, Is.EqualTo(0.25m));
            Assert.That(result.Single(r => r.Title == "Standup").TotalHours, Is.EqualTo(0.25m));
        }

        [Test]
        public void LastEntryStillRunning_ContributesNull()
        {
            // The original LEFT JOIN produced NULL for the final open entry.
            var result = Summarise(Entry("Admin", "09:00", null, 1));

            Assert.That(result.Single().TotalHours, Is.Null);
        }

        [Test]
        public void RepeatedTitles_AreSummed()
        {
            var result = Summarise(
                Entry("Admin", "09:00", "10:00", 1),
                Entry("Standup", "10:00", "10:15", 2),
                Entry("Admin", "10:15", "11:15", 3));

            Assert.That(result.Single(r => r.Title == "Admin").TotalHours, Is.EqualTo(2.0m));
        }

        [Test]
        public void SumIgnoresNull_WhenAnotherOccurrenceHasHours()
        {
            // SQL SUM() skips NULLs unless every value is NULL.
            var result = Summarise(
                Entry("Admin", "09:00", "10:00", 1),
                Entry("Standup", "10:00", "10:30", 2),
                Entry("Admin", "10:30", null, 3));

            Assert.That(result.Single(r => r.Title == "Admin").TotalHours, Is.EqualTo(1.0m));
        }

        [Test]
        public void QuarterHours_ProduceExactDecimals()
        {
            var result = Summarise(
                Entry("Admin", "09:00", "09:15", 1),
                Entry("Admin", "09:15", "09:30", 2),
                Entry("Admin", "09:30", "09:45", 3),
                Entry("Admin", "09:45", "10:00", 4));

            Assert.That(result.Single().TotalHours, Is.EqualTo(1.0m));
        }

        [Test]
        public void SecondsAreTruncated_MatchingDatediffMinute()
        {
            // DATEDIFF(MINUTE, ...) counts minute boundaries, so seconds are ignored.
            var entry = new TimeEntry
            {
                Title = "Admin",
                StartTime = Day.AddHours(9).AddSeconds(59),
                EndTime = Day.AddHours(10)
            };

            Assert.That(TimeReportingContext.SummariseTimesheet([entry]).Single().TotalHours,
                Is.EqualTo(1.0m));
        }

        [Test]
        public void EmptyDay_ReturnsNoRows()
        {
            Assert.That(TimeReportingContext.SummariseTimesheet([]), Is.Empty);
        }

        [Test]
        public void Results_AreOrderedByTitle()
        {
            var result = Summarise(
                Entry("Zebra", "09:00", "09:15", 1),
                Entry("Admin", "09:15", "09:30", 2),
                Entry("Middle", "09:30", "09:45", 3));

            Assert.That(result.Select(r => r.Title), Is.EqualTo(new[] { "Admin", "Middle", "Zebra" }));
        }
    }
}
