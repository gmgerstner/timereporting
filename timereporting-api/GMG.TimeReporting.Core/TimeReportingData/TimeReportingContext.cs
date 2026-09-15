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

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<DateTime>()
                .HaveConversion<UnspecifiedKindConverter>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CommonTask>(entity =>
            {
                entity.ToTable("CommonTasks");

                entity.HasKey(e => e.CommonTaskId);

                entity.Property(e => e.CommonTaskId).UseIdentityByDefaultColumn();

                entity.Property(e => e.Title)
                    .IsRequired()
                    .HasMaxLength(100);
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

                // The daily timesheet and schedule queries both filter and sort on StartTime.
                entity.HasIndex(e => e.StartTime);
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
