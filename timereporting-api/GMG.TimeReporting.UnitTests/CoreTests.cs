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
                .UseSqlServer(connectionString)
                .Options;
            context = new TimeReportingContext(options);
        }

        [TearDown]
        public void TearDown()
        {
            context?.Dispose();
        }

        [Test]
        public void LastFewTimeEntries()
        {
            var count = context.TimeEntries.Count();
            Assert.That(count, Is.GreaterThan(0));
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
