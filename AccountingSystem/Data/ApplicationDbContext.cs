using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AccountingSystem.Models;

namespace AccountingSystem.Data
{
    public class ApplicationDbContext : IdentityDbContext<User>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Branch> Branches { get; set; }
        public DbSet<CostCenter> CostCenters { get; set; }
        public DbSet<Account> Accounts { get; set; }
        public DbSet<JournalEntry> JournalEntries { get; set; }
        public DbSet<JournalEntryLine> JournalEntryLines { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<UserBranch> UserBranches { get; set; }
        public DbSet<UserPermission> UserPermissions { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Branch configuration
            builder.Entity<Branch>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Code).IsUnique();
                entity.Property(e => e.Code).IsRequired().HasMaxLength(10);
                entity.Property(e => e.NameAr).IsRequired().HasMaxLength(200);
                entity.Property(e => e.NameEn).HasMaxLength(200);
            });

            // CostCenter configuration
            builder.Entity<CostCenter>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Code).IsUnique();
                entity.Property(e => e.Code).IsRequired().HasMaxLength(10);
                entity.Property(e => e.NameAr).IsRequired().HasMaxLength(200);
                entity.Property(e => e.NameEn).HasMaxLength(200);
                
                entity.HasOne(e => e.Parent)
                    .WithMany(e => e.Children)
                    .HasForeignKey(e => e.ParentId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Account configuration
            builder.Entity<Account>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Code).IsUnique();
                entity.Property(e => e.Code).IsRequired().HasMaxLength(20);
                entity.Property(e => e.NameAr).IsRequired().HasMaxLength(200);
                entity.Property(e => e.NameEn).HasMaxLength(200);
                entity.Property(e => e.OpeningBalance).HasColumnType("decimal(18,2)");
                
                entity.HasOne(e => e.Parent)
                    .WithMany(e => e.Children)
                    .HasForeignKey(e => e.ParentId)
                    .OnDelete(DeleteBehavior.Restrict);
                
                entity.HasOne(e => e.Branch)
                    .WithMany(e => e.Accounts)
                    .HasForeignKey(e => e.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // JournalEntry configuration
            builder.Entity<JournalEntry>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Number).IsUnique();
                entity.Property(e => e.Number).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
                entity.Property(e => e.TotalDebit).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalCredit).HasColumnType("decimal(18,2)");
                
                entity.HasOne(e => e.Branch)
                    .WithMany(e => e.JournalEntries)
                    .HasForeignKey(e => e.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);
                
                entity.HasOne(e => e.CreatedBy)
                    .WithMany(e => e.CreatedJournalEntries)
                    .HasForeignKey(e => e.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // JournalEntryLine configuration
            builder.Entity<JournalEntryLine>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.DebitAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.CreditAmount).HasColumnType("decimal(18,2)");
                
                entity.HasOne(e => e.JournalEntry)
                    .WithMany(e => e.Lines)
                    .HasForeignKey(e => e.JournalEntryId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(e => e.Account)
                    .WithMany(e => e.JournalEntryLines)
                    .HasForeignKey(e => e.AccountId)
                    .OnDelete(DeleteBehavior.Restrict);
                
                entity.HasOne(e => e.CostCenter)
                    .WithMany(e => e.JournalEntryLines)
                    .HasForeignKey(e => e.CostCenterId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Permission configuration
            builder.Entity<Permission>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Name).IsUnique();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
            });

            // UserBranch configuration
            builder.Entity<UserBranch>(entity =>
            {
                entity.HasKey(e => new { e.UserId, e.BranchId });
                
                entity.HasOne(e => e.User)
                    .WithMany(e => e.UserBranches)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(e => e.Branch)
                    .WithMany(e => e.UserBranches)
                    .HasForeignKey(e => e.BranchId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // UserPermission configuration
            builder.Entity<UserPermission>(entity =>
            {
                entity.HasKey(e => new { e.UserId, e.PermissionId });
                
                entity.HasOne(e => e.User)
                    .WithMany(e => e.UserPermissions)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(e => e.Permission)
                    .WithMany(e => e.UserPermissions)
                    .HasForeignKey(e => e.PermissionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Seed data
            SeedData(builder);
        }

        private void SeedData(ModelBuilder builder)
        {
            // Seed default permissions
            builder.Entity<Permission>().HasData(
                new Permission { Id = 1, Name = "users.view", DisplayName = "عرض المستخدمين", Category = "المستخدمين" },
                new Permission { Id = 2, Name = "users.create", DisplayName = "إنشاء المستخدمين", Category = "المستخدمين" },
                new Permission { Id = 3, Name = "users.edit", DisplayName = "تعديل المستخدمين", Category = "المستخدمين" },
                new Permission { Id = 4, Name = "users.delete", DisplayName = "حذف المستخدمين", Category = "المستخدمين" },
                
                new Permission { Id = 5, Name = "branches.view", DisplayName = "عرض الفروع", Category = "الفروع" },
                new Permission { Id = 6, Name = "branches.create", DisplayName = "إنشاء الفروع", Category = "الفروع" },
                new Permission { Id = 7, Name = "branches.edit", DisplayName = "تعديل الفروع", Category = "الفروع" },
                new Permission { Id = 8, Name = "branches.delete", DisplayName = "حذف الفروع", Category = "الفروع" },
                
                new Permission { Id = 9, Name = "costcenters.view", DisplayName = "عرض مراكز التكلفة", Category = "مراكز التكلفة" },
                new Permission { Id = 10, Name = "costcenters.create", DisplayName = "إنشاء مراكز التكلفة", Category = "مراكز التكلفة" },
                new Permission { Id = 11, Name = "costcenters.edit", DisplayName = "تعديل مراكز التكلفة", Category = "مراكز التكلفة" },
                new Permission { Id = 12, Name = "costcenters.delete", DisplayName = "حذف مراكز التكلفة", Category = "مراكز التكلفة" },
                
                new Permission { Id = 13, Name = "accounts.view", DisplayName = "عرض الحسابات", Category = "الحسابات" },
                new Permission { Id = 14, Name = "accounts.create", DisplayName = "إنشاء الحسابات", Category = "الحسابات" },
                new Permission { Id = 15, Name = "accounts.edit", DisplayName = "تعديل الحسابات", Category = "الحسابات" },
                new Permission { Id = 16, Name = "accounts.delete", DisplayName = "حذف الحسابات", Category = "الحسابات" },
                
                new Permission { Id = 17, Name = "journal.view", DisplayName = "عرض القيود", Category = "القيود المالية" },
                new Permission { Id = 18, Name = "journal.create", DisplayName = "إنشاء القيود", Category = "القيود المالية" },
                new Permission { Id = 19, Name = "journal.edit", DisplayName = "تعديل القيود", Category = "القيود المالية" },
                new Permission { Id = 20, Name = "journal.delete", DisplayName = "حذف القيود", Category = "القيود المالية" },
                new Permission { Id = 21, Name = "journal.approve", DisplayName = "اعتماد القيود", Category = "القيود المالية" },
                
                new Permission { Id = 22, Name = "reports.view", DisplayName = "عرض التقارير", Category = "التقارير" },
                new Permission { Id = 23, Name = "reports.export", DisplayName = "تصدير التقارير", Category = "التقارير" },
                
                new Permission { Id = 24, Name = "dashboard.view", DisplayName = "عرض لوحة التحكم", Category = "لوحة التحكم" }
            );
        }
    }
}

