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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Category>(entity =>
            {
                entity.ToTable("Categories");

                entity.HasKey(e => e.CategoryId);

                entity.Property(e => e.CategoryName)
                    .IsRequired()
                    .HasMaxLength(50)
                    .IsUnicode(false);
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

                entity.Property(e => e.CreatedDate).HasColumnType("datetime");

                entity.Property(e => e.ExpirationDate).HasColumnType("datetime");

                entity.Property(e => e.LastModifiedDate).HasColumnType("datetime");

                entity.Property(e => e.Notes).HasColumnType("text");

                entity.Property(e => e.PasswordValue)
                    .HasColumnName("Password")
                    .IsRequired()
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.Title)
                    .IsRequired()
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.Url)
                    .HasColumnName("URL")
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.Username)
                    .HasMaxLength(50)
                    .IsUnicode(false);

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
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.SystemPasswordHint)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.HashedSystemPassword)
                    .IsRequired()
                    .HasMaxLength(64)
                    .IsUnicode(false)
                    .IsFixedLength();

                entity.Property(e => e.SystemUsername)
                    .IsRequired()
                    .HasMaxLength(50)
                    .IsUnicode(false);
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
