using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using AccountingSystem.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace AccountingSystem.Data
{
    public class ApplicationDbContext : IdentityDbContext<User>
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IHttpContextAccessor httpContextAccessor)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public DbSet<Branch> Branches { get; set; }
        public DbSet<CostCenter> CostCenters { get; set; }
        public DbSet<Account> Accounts { get; set; }
        public DbSet<JournalEntry> JournalEntries { get; set; }
        public DbSet<JournalEntryLine> JournalEntryLines { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<UserBranch> UserBranches { get; set; }
        public DbSet<UserPermission> UserPermissions { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<PaymentTransfer> PaymentTransfers { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<CashBoxClosure> CashBoxClosures { get; set; }
        public DbSet<Currency> Currencies { get; set; }
        public DbSet<ReceiptVoucher> ReceiptVouchers { get; set; }
        public DbSet<DisbursementVoucher> DisbursementVouchers { get; set; }

        public override int SaveChanges()
        {
            return SaveChangesAsync().GetAwaiter().GetResult();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ChangeTracker.DetectChanges();
            var auditEntries = new List<(AuditLog Log, EntityEntry Entry)>();

            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                    continue;

                var keyProperty = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
                var recordId = keyProperty?.CurrentValue?.ToString();
                var tableName = entry.Metadata.GetTableName();
                var operation = entry.State.ToString();

                foreach (var property in entry.Properties)
                {
                    if (entry.State == EntityState.Modified && !property.IsModified)
                        continue;

                    var log = new AuditLog
                    {
                        UserId = _httpContextAccessor?.HttpContext?.User?.Identity?.Name,
                        Timestamp = DateTime.Now,
                        TableName = tableName,
                        Operation = operation,
                        RecordId = recordId,
                        ColumnName = property.Metadata.Name
                    };

                    if (entry.State == EntityState.Modified)
                    {
                        log.OldValues = JsonSerializer.Serialize(property.OriginalValue);
                        log.NewValues = JsonSerializer.Serialize(property.CurrentValue);
                    }
                    else if (entry.State == EntityState.Added)
                    {
                        log.NewValues = JsonSerializer.Serialize(property.CurrentValue);
                    }
                    else if (entry.State == EntityState.Deleted)
                    {
                        log.OldValues = JsonSerializer.Serialize(property.OriginalValue);
                    }

                    auditEntries.Add((log, entry));
                }
            }

            var result = await base.SaveChangesAsync(cancellationToken);

            if (auditEntries.Count > 0)
            {
                foreach (var (log, entry) in auditEntries)
                {
                    if (string.IsNullOrEmpty(log.RecordId))
                    {
                        var keyProperty = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
                        if (keyProperty?.CurrentValue != null)
                            log.RecordId = keyProperty.CurrentValue.ToString();
                    }
                }

                AuditLogs.AddRange(auditEntries.Select(a => a.Log));
                await base.SaveChangesAsync(cancellationToken);
            }

            return result;
        }

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

            // Currency configuration
            builder.Entity<Currency>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Code).IsUnique();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Code).IsRequired().HasMaxLength(3);
                entity.Property(e => e.ExchangeRate).HasColumnType("decimal(18,6)");
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
                entity.Property(e => e.CurrencyId);

                entity.HasOne(e => e.Parent)
                    .WithMany(e => e.Children)
                    .HasForeignKey(e => e.ParentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Branch)
                    .WithMany(e => e.Accounts)
                    .HasForeignKey(e => e.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Currency)
                    .WithMany(e => e.Accounts)
                    .HasForeignKey(e => e.CurrencyId)
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

            // CashBoxClosure configuration
            builder.Entity<CashBoxClosure>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CountedAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.OpeningBalance).HasColumnType("decimal(18,2)");
                entity.Property(e => e.ClosingBalance).HasColumnType("decimal(18,2)");

                entity.HasOne(e => e.User)
                    .WithMany(u => u.CashBoxClosures)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Account)
                    .WithMany()
                    .HasForeignKey(e => e.AccountId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Branch)
                    .WithMany()
                    .HasForeignKey(e => e.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Expense configuration
            builder.Entity<Expense>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");

                entity.HasOne(e => e.User)
                    .WithMany(u => u.Expenses)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.PaymentAccount)
                    .WithMany()
                    .HasForeignKey(e => e.PaymentAccountId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.ExpenseAccount)
                    .WithMany()
                    .HasForeignKey(e => e.ExpenseAccountId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Branch)
                    .WithMany()
                    .HasForeignKey(e => e.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.JournalEntry)
                    .WithMany()
                    .HasForeignKey(e => e.JournalEntryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<PaymentTransfer>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Notes).HasMaxLength(500);

                entity.HasOne(e => e.Sender)
                    .WithMany()
                    .HasForeignKey(e => e.SenderId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Receiver)
                    .WithMany()
                    .HasForeignKey(e => e.ReceiverId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.FromPaymentAccount)
                    .WithMany()
                    .HasForeignKey(e => e.FromPaymentAccountId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.ToPaymentAccount)
                    .WithMany()
                    .HasForeignKey(e => e.ToPaymentAccountId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.FromBranch)
                    .WithMany()
                    .HasForeignKey(e => e.FromBranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.ToBranch)
                    .WithMany()
                    .HasForeignKey(e => e.ToBranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.JournalEntry)
                    .WithMany()
                    .HasForeignKey(e => e.JournalEntryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ReceiptVoucher configuration
            builder.Entity<ReceiptVoucher>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.ExchangeRate).HasColumnType("decimal(18,6)");
                entity.HasOne(e => e.Account)
                    .WithMany()
                    .HasForeignKey(e => e.AccountId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Currency)
                    .WithMany()
                    .HasForeignKey(e => e.CurrencyId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // DisbursementVoucher configuration
            builder.Entity<DisbursementVoucher>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.ExchangeRate).HasColumnType("decimal(18,6)");
                entity.HasOne(e => e.Account)
                    .WithMany()
                    .HasForeignKey(e => e.AccountId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Currency)
                    .WithMany()
                    .HasForeignKey(e => e.CurrencyId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<User>(entity =>
            {
                entity.HasOne(u => u.PaymentAccount)
                    .WithMany()
                    .HasForeignKey(u => u.PaymentAccountId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(u => u.PaymentBranch)
                    .WithMany()
                    .HasForeignKey(u => u.PaymentBranchId)
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
            builder.Entity<Currency>().HasData(
                new Currency { Id = 1, Name = "US Dollar", Code = "USD", ExchangeRate = 1m, IsBase = true }
            );

            // Seed default permissions with deterministic CreatedAt values to avoid
            // model changes between builds.
            var createdAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            builder.Entity<Permission>().HasData(
                new Permission { Id = 1, Name = "users.view", DisplayName = "عرض المستخدمين", Category = "المستخدمين", CreatedAt = createdAt },
                new Permission { Id = 2, Name = "users.create", DisplayName = "إنشاء المستخدمين", Category = "المستخدمين", CreatedAt = createdAt },
                new Permission { Id = 3, Name = "users.edit", DisplayName = "تعديل المستخدمين", Category = "المستخدمين", CreatedAt = createdAt },
                new Permission { Id = 4, Name = "users.delete", DisplayName = "حذف المستخدمين", Category = "المستخدمين", CreatedAt = createdAt },

                new Permission { Id = 5, Name = "branches.view", DisplayName = "عرض الفروع", Category = "الفروع", CreatedAt = createdAt },
                new Permission { Id = 6, Name = "branches.create", DisplayName = "إنشاء الفروع", Category = "الفروع", CreatedAt = createdAt },
                new Permission { Id = 7, Name = "branches.edit", DisplayName = "تعديل الفروع", Category = "الفروع", CreatedAt = createdAt },
                new Permission { Id = 8, Name = "branches.delete", DisplayName = "حذف الفروع", Category = "الفروع", CreatedAt = createdAt },

                new Permission { Id = 9, Name = "costcenters.view", DisplayName = "عرض مراكز التكلفة", Category = "مراكز التكلفة", CreatedAt = createdAt },
                new Permission { Id = 10, Name = "costcenters.create", DisplayName = "إنشاء مراكز التكلفة", Category = "مراكز التكلفة", CreatedAt = createdAt },
                new Permission { Id = 11, Name = "costcenters.edit", DisplayName = "تعديل مراكز التكلفة", Category = "مراكز التكلفة", CreatedAt = createdAt },
                new Permission { Id = 12, Name = "costcenters.delete", DisplayName = "حذف مراكز التكلفة", Category = "مراكز التكلفة", CreatedAt = createdAt },

                new Permission { Id = 13, Name = "accounts.view", DisplayName = "عرض الحسابات", Category = "الحسابات", CreatedAt = createdAt },
                new Permission { Id = 14, Name = "accounts.create", DisplayName = "إنشاء الحسابات", Category = "الحسابات", CreatedAt = createdAt },
                new Permission { Id = 15, Name = "accounts.edit", DisplayName = "تعديل الحسابات", Category = "الحسابات", CreatedAt = createdAt },
                new Permission { Id = 16, Name = "accounts.delete", DisplayName = "حذف الحسابات", Category = "الحسابات", CreatedAt = createdAt },

                new Permission { Id = 17, Name = "journal.view", DisplayName = "عرض القيود", Category = "القيود المالية", CreatedAt = createdAt },
                new Permission { Id = 18, Name = "journal.create", DisplayName = "إنشاء القيود", Category = "القيود المالية", CreatedAt = createdAt },
                new Permission { Id = 19, Name = "journal.edit", DisplayName = "تعديل القيود", Category = "القيود المالية", CreatedAt = createdAt },
                new Permission { Id = 20, Name = "journal.delete", DisplayName = "حذف القيود", Category = "القيود المالية", CreatedAt = createdAt },
                new Permission { Id = 21, Name = "journal.approve", DisplayName = "اعتماد القيود", Category = "القيود المالية", CreatedAt = createdAt },

                new Permission { Id = 22, Name = "reports.view", DisplayName = "عرض التقارير", Category = "التقارير", CreatedAt = createdAt },
                new Permission { Id = 23, Name = "reports.export", DisplayName = "تصدير التقارير", Category = "التقارير", CreatedAt = createdAt },

                new Permission { Id = 24, Name = "dashboard.view", DisplayName = "عرض لوحة التحكم", Category = "لوحة التحكم", CreatedAt = createdAt },
                new Permission { Id = 25, Name = "dashboard.widget.stats", DisplayName = "عرض إحصائيات لوحة التحكم", Category = "لوحة التحكم", CreatedAt = createdAt },
                new Permission { Id = 26, Name = "dashboard.widget.accounts", DisplayName = "عرض أرصدة الحسابات بلوحة التحكم", Category = "لوحة التحكم", CreatedAt = createdAt },
                new Permission { Id = 27, Name = "dashboard.widget.links", DisplayName = "عرض الروابط السريعة بلوحة التحكم", Category = "لوحة التحكم", CreatedAt = createdAt },
                new Permission { Id = 28, Name = "expenses.view", DisplayName = "عرض المصاريف", Category = "المصاريف", CreatedAt = createdAt },
                new Permission { Id = 29, Name = "expenses.create", DisplayName = "إنشاء المصاريف", Category = "المصاريف", CreatedAt = createdAt },
                new Permission { Id = 30, Name = "expenses.edit", DisplayName = "تعديل المصاريف", Category = "المصاريف", CreatedAt = createdAt },
                new Permission { Id = 31, Name = "expenses.delete", DisplayName = "حذف المصاريف", Category = "المصاريف", CreatedAt = createdAt },
                new Permission { Id = 32, Name = "expenses.approve", DisplayName = "اعتماد المصاريف", Category = "المصاريف", CreatedAt = createdAt },
                new Permission { Id = 33, Name = "transfers.view", DisplayName = "عرض الحوالات", Category = "الحوالات", CreatedAt = createdAt },
                new Permission { Id = 34, Name = "transfers.create", DisplayName = "إنشاء الحوالات", Category = "الحوالات", CreatedAt = createdAt },
                new Permission { Id = 35, Name = "transfers.approve", DisplayName = "اعتماد الحوالات", Category = "الحوالات", CreatedAt = createdAt },
                new Permission { Id = 36, Name = "currencies.view", DisplayName = "عرض العملات", Category = "العملات", CreatedAt = createdAt },
                new Permission { Id = 37, Name = "currencies.create", DisplayName = "إنشاء العملات", Category = "العملات", CreatedAt = createdAt },
                new Permission { Id = 38, Name = "currencies.edit", DisplayName = "تعديل العملات", Category = "العملات", CreatedAt = createdAt },
                new Permission { Id = 39, Name = "currencies.delete", DisplayName = "حذف العملات", Category = "العملات", CreatedAt = createdAt }
            );
        }
    }
}

