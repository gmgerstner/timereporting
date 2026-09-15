using GMG.TimeReporting.Core.TimeReportingData;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace GMG.TimeReporting.UnitTests
{
    /// <summary>
    /// Proves that one user's queries cannot see another user's rows. Database-backed,
    /// because the point is what the generated SQL returns, not what LINQ-to-objects does.
    /// </summary>
    public class MultiUserTests
    {
        private const string ConnectionStringVariable = "TIMEREPORTING_TEST_CONNECTION";

        private TimeReportingContext context = null!;
        private User alice = null!;
        private User bob = null!;

        [SetUp]
        public async Task Setup()
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

            // Unique names so repeated runs, and runs against a database that already has
            // real users in it, never collide.
            var suffix = Guid.NewGuid().ToString("n")[..12];
            alice = await AddUserAsync($"alice-{suffix}");
            bob = await AddUserAsync($"bob-{suffix}");

            var day = DateTime.Today;
            context.TimeEntries.AddRange(
                new TimeEntry { UserId = alice.UserId, Title = "Alice admin", StartTime = day.AddHours(9), EndTime = day.AddHours(10) },
                new TimeEntry { UserId = alice.UserId, Title = "Alice standup", StartTime = day.AddHours(10), EndTime = day.AddHours(10).AddMinutes(30) },
                new TimeEntry { UserId = bob.UserId, Title = "Bob admin", StartTime = day.AddHours(9), EndTime = day.AddHours(13) });
            context.CommonTasks.AddRange(
                new CommonTask { UserId = alice.UserId, Title = "Alice favourite" },
                new CommonTask { UserId = bob.UserId, Title = "Bob favourite" });
            await context.SaveChangesAsync();
        }

        [TearDown]
        public async Task TearDown()
        {
            if (context is null) return;

            // Time entries and common tasks cascade from the user.
            context.Users.RemoveRange(context.Users.Where(u => u.UserId == alice.UserId || u.UserId == bob.UserId));
            await context.SaveChangesAsync();
            await context.DisposeAsync();
        }

        private async Task<User> AddUserAsync(string username)
        {
            var user = new User { Username = username, IsAdmin = false, CreatedDate = DateTime.Now };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        }

        [Test]
        public async Task DailyTimesheet_CoversOnlyTheRequestedUser()
        {
            var sheet = await context.GetDailyTimesheetAsync(alice.UserId, DateTime.Today);

            Assert.That(sheet.Select(entry => entry.Title), Is.EquivalentTo(new[] { "Alice admin", "Alice standup" }));
            Assert.That(sheet.Sum(entry => entry.TotalHours ?? 0m), Is.EqualTo(1.5m));
        }

        [Test]
        public async Task DailyTimesheet_DoesNotChainAcrossUsers()
        {
            // Alice's 10:00 entry must be closed off by her own next entry, never by Bob's.
            var openEntry = new TimeEntry
            {
                UserId = alice.UserId,
                Title = "Alice open",
                StartTime = DateTime.Today.AddHours(11),
                EndTime = null
            };
            context.TimeEntries.Add(openEntry);
            context.TimeEntries.Add(new TimeEntry
            {
                UserId = bob.UserId,
                Title = "Bob later",
                StartTime = DateTime.Today.AddHours(15),
                EndTime = DateTime.Today.AddHours(16)
            });
            await context.SaveChangesAsync();

            var sheet = await context.GetDailyTimesheetAsync(alice.UserId, DateTime.Today);
            var open = sheet.Single(entry => entry.Title == "Alice open");

            // Last entry of her day and still running, so it contributes no hours. If Bob's
            // 15:00 start had been allowed to close it, this would read 4.
            Assert.That(open.TotalHours, Is.Null);
        }

        [Test]
        public async Task RecentTasks_CoverOnlyTheRequestedUser()
        {
            var recent = await context.GetRecentTasksAsync(bob.UserId);

            Assert.That(recent.Select(task => task.Title), Is.EquivalentTo(new[] { "Bob admin" }));
        }

        [Test]
        public async Task CommonTasks_ArePerUser()
        {
            var aliceFavourites = await context.CommonTasks
                .Where(task => task.UserId == alice.UserId)
                .Select(task => task.Title)
                .ToListAsync();

            Assert.That(aliceFavourites, Is.EquivalentTo(new[] { "Alice favourite" }));
        }

        [Test]
        public async Task LookingUpAnotherUsersEntryById_FindsNothing()
        {
            // What TimeEntriesController does for Get/Edit/Delete: match on id AND user, so
            // Alice asking for Bob's entry gets the same miss as asking for one that is gone.
            var bobEntryId = await context.TimeEntries
                .Where(te => te.UserId == bob.UserId)
                .Select(te => te.TimeEntryId)
                .FirstAsync();

            var found = await context.TimeEntries
                .FirstOrDefaultAsync(te => te.TimeEntryId == bobEntryId && te.UserId == alice.UserId);

            Assert.That(found, Is.Null);
        }

        [Test]
        public async Task Usernames_AreUnique()
        {
            context.Users.Add(new User
            {
                Username = alice.Username,
                IsAdmin = false,
                CreatedDate = DateTime.Now
            });

            Assert.That(async () => await context.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
            context.ChangeTracker.Clear();
        }
    }
}
