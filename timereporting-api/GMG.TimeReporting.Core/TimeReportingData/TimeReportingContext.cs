using Microsoft.EntityFrameworkCore;

namespace GMG.TimeReporting.Core.TimeReportingData
{
    /// <summary>
    /// Code-first context for the TimeReporting database. The schema is owned by the
    /// migrations in <c>GMG.TimeReporting.Core/Migrations</c>; it replaces the former
    /// GMG.TimeReporting.Database SSDT project.
    /// </summary>
    public partial class TimeReportingContext : DbContext
    {
        public TimeReportingContext()
        {
        }

        public TimeReportingContext(DbContextOptions<TimeReportingContext> options)
            : base(options)
        {
        }

        public virtual DbSet<CommonTask> CommonTasks { get; set; } = null!;
        public virtual DbSet<TimeEntry> TimeEntries { get; set; } = null!;
        public virtual DbSet<User> Users { get; set; } = null!;

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<DateTime>()
                .HaveConversion<UnspecifiedKindConverter>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");

                entity.HasKey(e => e.UserId);

                entity.Property(e => e.UserId).UseIdentityByDefaultColumn();

                entity.Property(e => e.Username)
                    .IsRequired()
                    .HasMaxLength(50);

                // Usernames are stored lower-cased, so a plain unique index is enough to
                // keep one person from ending up with two accounts.
                entity.HasIndex(e => e.Username).IsUnique();

                entity.Property(e => e.CreatedDate).HasColumnType("timestamp without time zone");
            });

            modelBuilder.Entity<CommonTask>(entity =>
            {
                entity.ToTable("CommonTasks");

                entity.HasKey(e => e.CommonTaskId);

                entity.Property(e => e.CommonTaskId).UseIdentityByDefaultColumn();

                entity.Property(e => e.Title)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.HasOne(e => e.User)
                    .WithMany(u => u.CommonTasks)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Each user keeps their own favourites, and the picker reads the whole list.
                entity.HasIndex(e => e.UserId);
            });

            modelBuilder.Entity<TimeEntry>(entity =>
            {
                entity.ToTable("TimeEntries");

                entity.HasKey(e => e.TimeEntryId)
                    .HasName("PK_TimeEntries");

                entity.Property(e => e.TimeEntryId).UseIdentityByDefaultColumn();

                entity.Property(e => e.Title)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.StartTime).HasColumnType("timestamp without time zone");

                entity.Property(e => e.EndTime).HasColumnType("timestamp without time zone");

                entity.HasOne(e => e.User)
                    .WithMany(u => u.TimeEntries)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // The daily timesheet and schedule queries both filter and sort on StartTime,
                // and now always within one user, so the user column leads the index.
                entity.HasIndex(e => new { e.UserId, e.StartTime });
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
