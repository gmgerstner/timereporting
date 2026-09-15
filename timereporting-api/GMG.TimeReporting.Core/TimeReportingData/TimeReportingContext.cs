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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CommonTask>(entity =>
            {
                entity.ToTable("CommonTasks");

                entity.HasKey(e => e.CommonTaskId);

                entity.Property(e => e.CommonTaskId).UseIdentityColumn();

                entity.Property(e => e.Title)
                    .IsRequired()
                    .HasMaxLength(100)
                    .IsUnicode(false);
            });

            modelBuilder.Entity<TimeEntry>(entity =>
            {
                entity.ToTable("TimeEntries");

                entity.HasKey(e => e.TimeEntryId)
                    .HasName("PK_TimeEntries")
                    .IsClustered();

                entity.Property(e => e.TimeEntryId).UseIdentityColumn();

                entity.Property(e => e.Title)
                    .IsRequired()
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.StartTime).HasColumnType("datetime");

                entity.Property(e => e.EndTime).HasColumnType("datetime");

                // The daily timesheet and schedule queries both filter and sort on StartTime.
                entity.HasIndex(e => e.StartTime);
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
