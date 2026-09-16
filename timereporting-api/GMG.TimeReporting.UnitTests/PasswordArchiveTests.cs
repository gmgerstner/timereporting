using GMG.TimeReporting.Core.PasswordArchiveData;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace GMG.TimeReporting.UnitTests
{
    /// <summary>
    /// Pins the rule that the PasswordArchive database — which belongs to a separate security
    /// application — is read-only to this app.
    /// </summary>
    /// <remarks>
    /// No database is needed or touched: the context refuses the write before it ever opens a
    /// connection, which is the whole point. The connection string below is never connected to.
    /// </remarks>
    public class PasswordArchiveTests
    {
        private static PasswordArchiveContext NewContext()
        {
            var options = new DbContextOptionsBuilder<PasswordArchiveContext>()
                .UseSqlServer("Server=invalid.example;Database=never-connected;Trusted_Connection=True")
                .Options;
            return new PasswordArchiveContext(options);
        }

        [Test]
        public void SaveChanges_IsRefused()
        {
            using var context = NewContext();
            context.Passwords.Add(new Password { Title = "x", PasswordValue = "x" });

            Assert.That(() => context.SaveChanges(), Throws.InvalidOperationException);
        }

        [Test]
        public void SaveChangesAsync_IsRefused()
        {
            using var context = NewContext();
            context.Passwords.Add(new Password { Title = "x", PasswordValue = "x" });

            Assert.That(async () => await context.SaveChangesAsync(), Throws.InvalidOperationException);
        }

        [Test]
        public void ReadsDoNotTrack()
        {
            using var context = NewContext();

            // Nothing tracked means nothing for a stray save to flush, on top of the refusal.
            Assert.That(
                context.ChangeTracker.QueryTrackingBehavior,
                Is.EqualTo(QueryTrackingBehavior.NoTracking));
        }
    }
}
