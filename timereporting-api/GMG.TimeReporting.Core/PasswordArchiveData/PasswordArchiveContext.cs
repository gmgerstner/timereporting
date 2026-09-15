using Microsoft.EntityFrameworkCore;

namespace GMG.TimeReporting.Core.PasswordArchiveData
{
    public partial class PasswordArchiveContext : DbContext
    {
        public PasswordArchiveContext()
        {
        }

        public PasswordArchiveContext(DbContextOptions<PasswordArchiveContext> options)
            : base(options)
        {
        }

        public virtual DbSet<Category> Categories { get; set; } = null!;
        public virtual DbSet<PasswordCategory> PasswordCategories { get; set; } = null!;
        public virtual DbSet<Password> Passwords { get; set; } = null!;
        public virtual DbSet<SystemUser> SystemUsers { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Nothing read out of the archive is ever edited, so there is no reason to pay
            // for change tracking — and no tracked entity for a stray SaveChanges to write.
            optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<DateTime>()
                .HaveConversion<UnspecifiedKindConverter>();
        }

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">Always.</exception>
        public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
            throw ReadOnly();

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">Always.</exception>
        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default) =>
            throw ReadOnly();

        /// <summary>
        /// Writes seed data into a local stand-in archive. Development only.
        /// </summary>
        /// <remarks>
        /// The one sanctioned exception to this context being read-only. It exists so a
        /// developer can get a working log-in on a machine that has no archive, and is called
        /// from exactly one place: the Development seeding in <c>Program.cs</c>. Anything
        /// that runs against a real archive must go through the ordinary
        /// <see cref="SaveChanges()"/>, which refuses.
        /// </remarks>
        public Task<int> SaveDevelopmentSeedDataAsync(CancellationToken cancellationToken = default) =>
            // base. dispatches non-virtually, so this deliberately steps past the override above.
            base.SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);

        private static InvalidOperationException ReadOnly() =>
            new("The PasswordArchive database belongs to the security application; this app only "
                + "reads from it. Development seeding uses SaveDevelopmentSeedDataAsync instead.");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Category>(entity =>
            {
                entity.ToTable("Categories");

                entity.HasKey(e => e.CategoryId);

                entity.Property(e => e.CategoryName)
                    .IsRequired()
                    .HasMaxLength(50);
            });

            modelBuilder.Entity<PasswordCategory>(entity =>
            {
                entity.ToTable("PasswordCategories");

                entity.HasKey(e => new { e.PasswordId, e.CategoryId });

                entity.HasOne(d => d.Category)
                    .WithMany(p => p.PasswordCategories)
                    .HasForeignKey(d => d.CategoryId)
                    //.OnDelete(DeleteBehavior.ClientSetNull)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_PasswordCategories_Categories");

                entity.HasOne(d => d.Password)
                    .WithMany(p => p.PasswordCategories)
                    .HasForeignKey(d => d.PasswordId)
                    //.OnDelete(DeleteBehavior.ClientSetNull)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_PasswordCategories_Passwords");
            });

            modelBuilder.Entity<Password>(entity =>
            {
                entity.ToTable("Passwords");

                entity.HasKey(e => e.PasswordId);

                entity.Property(e => e.CreatedDate).HasColumnType("timestamp without time zone");

                entity.Property(e => e.ExpirationDate).HasColumnType("timestamp without time zone");

                entity.Property(e => e.LastModifiedDate).HasColumnType("timestamp without time zone");

                entity.Property(e => e.Notes).HasColumnType("text");

                entity.Property(e => e.PasswordValue)
                    .HasColumnName("Password")
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.Title)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.Url)
                    .HasColumnName("URL")
                    .HasMaxLength(50);

                entity.Property(e => e.Username)
                    .HasMaxLength(50);

                entity.HasOne(d => d.SystemUser)
                    .WithMany(p => p.Passwords)
                    .HasForeignKey(d => d.SystemUserId)
                    //.OnDelete(DeleteBehavior.ClientSetNull)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_Passwords_SystemUsers");
            });

            modelBuilder.Entity<SystemUser>(entity =>
            {
                entity.ToTable("SystemUsers");

                entity.HasKey(e => e.SystemUserId);

                entity.Property(e => e.DefaultUsername)
                    .HasMaxLength(50);

                entity.Property(e => e.SystemPasswordHint)
                    .HasMaxLength(50);

                entity.Property(e => e.HashedSystemPassword)
                    .IsRequired()
                    .HasMaxLength(64)
                    .IsFixedLength();

                entity.Property(e => e.SystemUsername)
                    .IsRequired()
                    .HasMaxLength(50);
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
