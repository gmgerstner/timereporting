using GMG.TimeReporting.Core;
using GMG.TimeReporting.Core.TimeReportingData;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace GMG.TimeReporting.UnitTests
{
    public class CoreTests
    {
        private const string ConnectionStringVariable = "TIMEREPORTING_TEST_CONNECTION";

        private TimeReportingContext context = null!;

        [SetUp]
        public void Setup()
        {
            var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Assert.Ignore($"Set the {ConnectionStringVariable} environment variable to run database-backed tests.");
            }

            var options = new DbContextOptionsBuilder<TimeReportingContext>()
                .UseNpgsql(connectionString)
                .Options;
            context = new TimeReportingContext(options);
        }

        [TearDown]
        public void TearDown()
        {
            context?.Dispose();
        }

        /// <summary>
        /// Smoke test that the mapped model still matches the database it is pointed at.
        /// </summary>
        /// <remarks>
        /// This used to assert the table was non-empty, which only held when the database
        /// happened to already have rows in it — and, now that entries belong to a user, a
        /// count across everyone says nothing useful. Running the query is the part that
        /// catches a column that the model and the schema disagree about.
        /// </remarks>
        [Test]
        public void TimeEntriesQuery_MatchesTheSchema()
        {
            Assert.That(() => context.TimeEntries.OrderBy(te => te.StartTime).Take(5).ToList(), Throws.Nothing);
        }
    }

    public class RulesTests
    {
        [Test]
        public void RoundToNearestQuarter_RoundsDownToPreviousQuarter()
        {
            var expected = new DateTime(2021, 1, 1, 11, 15, 0);
            var actual = new DateTime(2021, 1, 1, 11, 18, 0).RoundToNearestQuarter();

            Assert.That(actual, Is.EqualTo(expected));
        }
    }
}
