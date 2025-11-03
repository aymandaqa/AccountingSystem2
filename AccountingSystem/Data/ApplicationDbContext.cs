using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using AccountingSystem.Models;
using AccountingSystem.Models.CompoundJournals;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using AccountingSystem.Models.Workflows;
using AccountingSystem.Models.DynamicScreens;

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

        public DbSet<CusomerMappingAccount> CusomerMappingAccounts { get; set; }
        public DbSet<DriverMappingAccount> DriverMappingAccounts { get; set; }
        public DbSet<Branch> Branches { get; set; }
        public DbSet<CostCenter> CostCenters { get; set; }
        public DbSet<Account> Accounts { get; set; }
        public DbSet<JournalEntry> JournalEntries { get; set; }
        public DbSet<JournalEntryLine> JournalEntryLines { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<PermissionGroup> PermissionGroups { get; set; }
        public DbSet<PermissionGroupPermission> PermissionGroupPermissions { get; set; }
        public DbSet<UserBranch> UserBranches { get; set; }
        public DbSet<UserPermission> UserPermissions { get; set; }
        public DbSet<UserPermissionGroup> UserPermissionGroups { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<PaymentTransfer> PaymentTransfers { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<CashBoxClosure> CashBoxClosures { get; set; }
        public DbSet<Currency> Currencies { get; set; }
        public DbSet<CurrencyUnit> CurrencyUnits { get; set; }
        public DbSet<ReceiptVoucher> ReceiptVouchers { get; set; }
        public DbSet<DisbursementVoucher> DisbursementVouchers { get; set; }
        public DbSet<PaymentVoucher> PaymentVouchers { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<SupplierBranch> SupplierBranches { get; set; }
        public DbSet<SystemSetting> SystemSettings { get; set; }
        public DbSet<UserPaymentAccount> UserPaymentAccounts { get; set; }
        public DbSet<Asset> Assets { get; set; }
        public DbSet<AssetType> AssetTypes { get; set; }
        public DbSet<AssetExpense> AssetExpenses { get; set; }
        public DbSet<PivotReport> PivotReports { get; set; }
        public DbSet<ReportQuery> ReportQueries { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<PayrollBatch> PayrollBatches { get; set; }
        public DbSet<PayrollBatchLine> PayrollBatchLines { get; set; }
        public DbSet<PayrollBatchLineDeduction> PayrollBatchLineDeductions { get; set; }
        public DbSet<DeductionType> DeductionTypes { get; set; }
        public DbSet<EmployeeDeduction> EmployeeDeductions { get; set; }
        public DbSet<AllowanceType> AllowanceTypes { get; set; }
        public DbSet<EmployeeAllowance> EmployeeAllowances { get; set; }
        public DbSet<PayrollBatchLineAllowance> PayrollBatchLineAllowances { get; set; }
        public DbSet<SalaryPayment> SalaryPayments { get; set; }
        public DbSet<EmployeeAdvance> EmployeeAdvances { get; set; }
        public DbSet<WorkflowDefinition> WorkflowDefinitions { get; set; }
        public DbSet<WorkflowStep> WorkflowSteps { get; set; }
        public DbSet<WorkflowInstance> WorkflowInstances { get; set; }
        public DbSet<WorkflowAction> WorkflowActions { get; set; }
        public DbSet<DynamicScreenDefinition> DynamicScreenDefinitions { get; set; }
        public DbSet<DynamicScreenField> DynamicScreenFields { get; set; }
        public DbSet<DynamicScreenEntry> DynamicScreenEntries { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<CompoundJournalDefinition> CompoundJournalDefinitions { get; set; }
        public DbSet<CompoundJournalExecutionLog> CompoundJournalExecutionLogs { get; set; }
        public DbSet<Agent> Agents { get; set; }

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

                entity.HasOne(e => e.EmployeeParentAccount)
                    .WithMany()
                    .HasForeignKey(e => e.EmployeeParentAccountId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<SupplierBranch>(entity =>
            {
                entity.HasKey(e => new { e.SupplierId, e.BranchId });

                entity.HasOne(e => e.Supplier)
                    .WithMany(s => s.SupplierBranches)
                    .HasForeignKey(e => e.SupplierId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Branch)
                    .WithMany()
                    .HasForeignKey(e => e.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);
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

            builder.Entity<CurrencyUnit>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.ValueInBaseUnit).HasColumnType("decimal(18,6)");

                entity.HasOne(e => e.Currency)
                    .WithMany(c => c.Units)
                    .HasForeignKey(e => e.CurrencyId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Agent>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Address).HasMaxLength(500);

                entity.HasOne(e => e.Branch)
                    .WithMany()
                    .HasForeignKey(e => e.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Account)
                    .WithMany()
                    .HasForeignKey(e => e.AccountId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<ReceiptVoucher>(entity =>
            {
                entity.HasOne(v => v.WorkflowInstance)
                    .WithMany()
                    .HasForeignKey(v => v.WorkflowInstanceId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<DisbursementVoucher>(entity =>
            {
                entity.HasOne(v => v.WorkflowInstance)
                    .WithMany()
                    .HasForeignKey(v => v.WorkflowInstanceId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<WorkflowInstance>(entity =>
            {
                entity.HasOne(i => i.DocumentCurrency)
                    .WithMany()
                    .HasForeignKey(i => i.DocumentCurrencyId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<PaymentVoucher>(entity =>
            {
                entity.HasOne(e => e.Supplier)
                    .WithMany()
                    .HasForeignKey(e => e.SupplierId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<User>(entity =>
            {
                entity.HasOne(e => e.Agent)
                    .WithMany(e => e.Users)
                    .HasForeignKey(e => e.AgentId)
                    .OnDelete(DeleteBehavior.SetNull);
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
                entity.Property(e => e.CurrentBalance).HasColumnType("decimal(18,2)");
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

            builder.Entity<Employee>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Address).HasMaxLength(500);
                entity.Property(e => e.PhoneNumber).HasMaxLength(50);
                entity.Property(e => e.JobTitle).HasMaxLength(200);
                entity.Property(e => e.Salary).HasColumnType("decimal(18,2)");

                entity.HasOne(e => e.Branch)
                    .WithMany(b => b.Employees)
                    .HasForeignKey(e => e.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Account)
                    .WithMany()
                    .HasForeignKey(e => e.AccountId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(e => e.PayrollLines)
                    .WithOne(l => l.Employee)
                    .HasForeignKey(l => l.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(e => e.EmployeeDeductions)
                    .WithOne(d => d.Employee)
                    .HasForeignKey(d => d.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.EmployeeAllowances)
                    .WithOne(a => a.Employee)
                    .HasForeignKey(a => a.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<PayrollBatch>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.ReferenceNumber).HasMaxLength(200);
                entity.Property(e => e.Notes).HasMaxLength(500);

                entity.HasOne(e => e.Branch)
                    .WithMany()
                    .HasForeignKey(e => e.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.PaymentAccount)
                    .WithMany()
                    .HasForeignKey(e => e.PaymentAccountId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.CreatedBy)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.ConfirmedBy)
                    .WithMany()
                    .HasForeignKey(e => e.ConfirmedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<PayrollBatchLine>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.GrossAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.DeductionAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.AllowanceAmount).HasColumnType("decimal(18,2)");

                entity.HasOne(e => e.PayrollBatch)
                    .WithMany(b => b.Lines)
                    .HasForeignKey(e => e.PayrollBatchId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Employee)
                    .WithMany(e => e.PayrollLines)
                    .HasForeignKey(e => e.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Branch)
                    .WithMany()
                    .HasForeignKey(e => e.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasMany(e => e.Deductions)
                    .WithOne(d => d.PayrollLine)
                    .HasForeignKey(d => d.PayrollBatchLineId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.Allowances)
                    .WithOne(a => a.PayrollLine)
                    .HasForeignKey(a => a.PayrollBatchLineId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<PayrollBatchLineDeduction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Type).HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(250);

                entity.HasOne(e => e.DeductionType)
                    .WithMany(d => d.PayrollDeductions)
                    .HasForeignKey(e => e.DeductionTypeId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.Account)
                    .WithMany()
                    .HasForeignKey(e => e.AccountId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<PayrollBatchLineAllowance>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Type).HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(250);

                entity.HasOne(e => e.AllowanceType)
                    .WithMany(a => a.PayrollAllowances)
                    .HasForeignKey(e => e.AllowanceTypeId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.Account)
                    .WithMany()
                    .HasForeignKey(e => e.AccountId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<DeductionType>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(500);

                entity.HasOne(e => e.Account)
                    .WithMany()
                    .HasForeignKey(e => e.AccountId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<AllowanceType>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(500);

                entity.HasOne(e => e.Account)
                    .WithMany()
                    .HasForeignKey(e => e.AccountId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<EmployeeDeduction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Description).HasMaxLength(250);

                entity.HasOne(e => e.Employee)
                    .WithMany(e => e.EmployeeDeductions)
                    .HasForeignKey(e => e.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.DeductionType)
                    .WithMany(d => d.EmployeeDeductions)
                    .HasForeignKey(e => e.DeductionTypeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<EmployeeAllowance>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Description).HasMaxLength(250);

                entity.HasOne(e => e.Employee)
                    .WithMany(e => e.EmployeeAllowances)
                    .HasForeignKey(e => e.EmployeeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.AllowanceType)
                    .WithMany(a => a.EmployeeAllowances)
                    .HasForeignKey(e => e.AllowanceTypeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<SalaryPayment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.ReferenceNumber).HasMaxLength(50);

                entity.HasOne(e => e.Employee)
                    .WithMany()
                    .HasForeignKey(e => e.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.PaymentAccount)
                    .WithMany()
                    .HasForeignKey(e => e.PaymentAccountId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Branch)
                    .WithMany()
                    .HasForeignKey(e => e.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Currency)
                    .WithMany()
                    .HasForeignKey(e => e.CurrencyId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.CreatedBy)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.JournalEntry)
                    .WithMany()
                    .HasForeignKey(e => e.JournalEntryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<EmployeeAdvance>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.ReferenceNumber).HasMaxLength(50);

                entity.HasOne(e => e.Employee)
                    .WithMany()
                    .HasForeignKey(e => e.EmployeeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.PaymentAccount)
                    .WithMany()
                    .HasForeignKey(e => e.PaymentAccountId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Branch)
                    .WithMany()
                    .HasForeignKey(e => e.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Currency)
                    .WithMany()
                    .HasForeignKey(e => e.CurrencyId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.CreatedBy)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.JournalEntry)
                    .WithMany()
                    .HasForeignKey(e => e.JournalEntryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<AssetType>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);

                entity.HasOne(e => e.Account)
                    .WithMany()
                    .HasForeignKey(e => e.AccountId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Asset>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.AssetNumber).HasMaxLength(100);
                entity.Property(e => e.Notes).HasMaxLength(500);
                entity.Property(e => e.OpeningBalance).HasColumnType("decimal(18,2)");

                entity.HasOne(e => e.Branch)
                    .WithMany(b => b.Assets)
                    .HasForeignKey(e => e.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Account)
                    .WithMany()
                    .HasForeignKey(e => e.AccountId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.AssetType)
                    .WithMany(t => t.Assets)
                    .HasForeignKey(e => e.AssetTypeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.CostCenter)
                    .WithMany()
                    .HasForeignKey(e => e.CostCenterId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<AssetExpense>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Notes).HasMaxLength(500);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.ExchangeRate).HasColumnType("decimal(18,6)");

                entity.HasOne(e => e.Asset)
                    .WithMany(a => a.Expenses)
                    .HasForeignKey(e => e.AssetId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.ExpenseAccount)
                    .WithMany()
                    .HasForeignKey(e => e.ExpenseAccountId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Account)
                    .WithMany()
                    .HasForeignKey(e => e.AccountId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Supplier)
                    .WithMany()
                    .HasForeignKey(e => e.SupplierId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Currency)
                    .WithMany()
                    .HasForeignKey(e => e.CurrencyId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.CreatedBy)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<PivotReport>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
                entity.Property(e => e.Layout).IsRequired();
                entity.Property(e => e.ReportType).IsRequired();
                entity.Property(e => e.CreatedById).IsRequired();
                entity.Property(e => e.CreatedAt).HasColumnType("datetime2");
                entity.Property(e => e.UpdatedAt).HasColumnType("datetime2");

                entity.HasOne(e => e.CreatedBy)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedById)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ReportQuery>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
                entity.Property(e => e.Description).HasMaxLength(250);
                entity.Property(e => e.DatasetKey).IsRequired().HasMaxLength(100);
                entity.Property(e => e.RulesJson).IsRequired();
                entity.Property(e => e.SelectedColumnsJson);
                entity.Property(e => e.CreatedAt).HasColumnType("datetime2");
                entity.Property(e => e.UpdatedAt).HasColumnType("datetime2");

                entity.HasOne(e => e.CreatedBy)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedById)
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

                entity.HasOne(e => e.SenderJournalEntry)
                    .WithMany()
                    .HasForeignKey(e => e.SenderJournalEntryId)
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
                entity.HasOne(e => e.PaymentAccount)
                    .WithMany()
                    .HasForeignKey(e => e.PaymentAccountId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Supplier)
                    .WithMany()
                    .HasForeignKey(e => e.SupplierId)
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
                entity.HasOne(e => e.Supplier)
                    .WithMany()
                    .HasForeignKey(e => e.SupplierId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Account)
                    .WithMany()
                    .HasForeignKey(e => e.AccountId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Currency)
                    .WithMany()
                    .HasForeignKey(e => e.CurrencyId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Supplier configuration
            builder.Entity<Supplier>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.NameAr).IsRequired().HasMaxLength(200);
                entity.Property(e => e.NameEn).HasMaxLength(200);
                entity.Property(e => e.Phone).HasMaxLength(200);
                entity.Property(e => e.Email).HasMaxLength(200);

                entity.HasOne(e => e.Account)
                    .WithMany()
                    .HasForeignKey(e => e.AccountId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // SystemSetting configuration
            builder.Entity<SystemSetting>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Key).IsUnique();
                entity.Property(e => e.Key).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Value).HasMaxLength(500);
            });

            builder.Entity<User>(entity =>
            {
                entity.Property(u => u.ExpenseLimit).HasColumnType("decimal(18,2)");

                entity.HasOne(u => u.PaymentAccount)
                    .WithMany()
                    .HasForeignKey(u => u.PaymentAccountId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(u => u.PaymentBranch)
                    .WithMany()
                    .HasForeignKey(u => u.PaymentBranchId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<UserPaymentAccount>(entity =>
            {
                entity.HasKey(e => new { e.UserId, e.CurrencyId });

                entity.HasOne(e => e.User)
                    .WithMany(u => u.UserPaymentAccounts)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Currency)
                    .WithMany()
                    .HasForeignKey(e => e.CurrencyId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Account)
                    .WithMany()
                    .HasForeignKey(e => e.AccountId)
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

            builder.Entity<PermissionGroup>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Name).IsUnique();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            builder.Entity<PermissionGroupPermission>(entity =>
            {
                entity.HasKey(e => new { e.PermissionGroupId, e.PermissionId });

                entity.HasOne(e => e.PermissionGroup)
                    .WithMany(e => e.PermissionGroupPermissions)
                    .HasForeignKey(e => e.PermissionGroupId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Permission)
                    .WithMany(e => e.PermissionGroupPermissions)
                    .HasForeignKey(e => e.PermissionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<UserPermissionGroup>(entity =>
            {
                entity.HasKey(e => new { e.UserId, e.PermissionGroupId });

                entity.Property(e => e.AssignedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasOne(e => e.User)
                    .WithMany(e => e.UserPermissionGroups)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.PermissionGroup)
                    .WithMany(e => e.UserPermissionGroups)
                    .HasForeignKey(e => e.PermissionGroupId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<WorkflowDefinition>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.HasMany(e => e.Steps)
                    .WithOne(e => e.WorkflowDefinition)
                    .HasForeignKey(e => e.WorkflowDefinitionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<WorkflowStep>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.RequiredPermission).HasMaxLength(200);
                entity.HasOne(e => e.ApproverUser)
                    .WithMany()
                    .HasForeignKey(e => e.ApproverUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Branch)
                    .WithMany()
                    .HasForeignKey(e => e.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<WorkflowInstance>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.WorkflowDefinition)
                    .WithMany()
                    .HasForeignKey(e => e.WorkflowDefinitionId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Initiator)
                    .WithMany()
                    .HasForeignKey(e => e.InitiatorId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<WorkflowAction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.WorkflowInstance)
                    .WithMany(e => e.Actions)
                    .HasForeignKey(e => e.WorkflowInstanceId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.WorkflowStep)
                    .WithMany(e => e.Actions)
                    .HasForeignKey(e => e.WorkflowStepId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<DynamicScreenDefinition>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Name).IsUnique();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
                entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.PermissionName).IsRequired().HasMaxLength(150);
                entity.Property(e => e.ManagePermissionName).HasMaxLength(150);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.LayoutDefinition).HasColumnType("nvarchar(max)");

                entity.HasOne(e => e.WorkflowDefinition)
                    .WithMany()
                    .HasForeignKey(e => e.WorkflowDefinitionId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.CreatedBy)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.UpdatedBy)
                    .WithMany()
                    .HasForeignKey(e => e.UpdatedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<DynamicScreenField>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.ScreenId, e.FieldKey }).IsUnique();
                entity.Property(e => e.FieldKey).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Label).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Placeholder).HasMaxLength(200);
                entity.Property(e => e.HelpText).HasMaxLength(200);
                entity.Property(e => e.AllowedEntityIds).HasColumnType("nvarchar(max)");
                entity.Property(e => e.MetadataJson).HasColumnType("nvarchar(max)");

                entity.HasOne(e => e.Screen)
                    .WithMany(e => e.Fields)
                    .HasForeignKey(e => e.ScreenId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<DynamicScreenEntry>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.DataJson).HasColumnType("nvarchar(max)");

                entity.HasOne(e => e.Screen)
                    .WithMany(e => e.Entries)
                    .HasForeignKey(e => e.ScreenId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.ExpenseAccount)
                    .WithMany()
                    .HasForeignKey(e => e.ExpenseAccountId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Supplier)
                    .WithMany()
                    .HasForeignKey(e => e.SupplierId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Branch)
                    .WithMany()
                    .HasForeignKey(e => e.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.CreatedBy)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.ApprovedBy)
                    .WithMany()
                    .HasForeignKey(e => e.ApprovedById)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.RejectedBy)
                    .WithMany()
                    .HasForeignKey(e => e.RejectedById)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.WorkflowInstance)
                    .WithMany()
                    .HasForeignKey(e => e.WorkflowInstanceId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            builder.Entity<UserSession>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.SessionId).IsUnique();
                entity.Property(e => e.SessionId).IsRequired().HasMaxLength(100);
                entity.Property(e => e.DeviceType).HasMaxLength(100);
                entity.Property(e => e.DeviceName).HasMaxLength(200);
                entity.Property(e => e.OperatingSystem).HasMaxLength(200);
                entity.Property(e => e.IpAddress).HasMaxLength(100);
                entity.Property(e => e.UserAgent).HasMaxLength(1000);
                entity.Property(e => e.EndedReason).HasMaxLength(200);

                entity.HasOne(e => e.User)
                    .WithMany(u => u.Sessions)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Notification>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Message).HasMaxLength(1000);
                entity.Property(e => e.Icon).HasMaxLength(100);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.WorkflowAction)
                    .WithMany()
                    .HasForeignKey(e => e.WorkflowActionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<CompoundJournalDefinition>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.TemplateJson).IsRequired();
                entity.HasOne(e => e.CreatedBy)
                    .WithMany()
                    .HasForeignKey(e => e.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<CompoundJournalExecutionLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Message).HasMaxLength(2000);
                entity.HasOne(e => e.Definition)
                    .WithMany(d => d.ExecutionLogs)
                    .HasForeignKey(e => e.DefinitionId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.JournalEntry)
                    .WithMany()
                    .HasForeignKey(e => e.JournalEntryId)
                    .OnDelete(DeleteBehavior.Restrict);
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
                new Permission { Id = 70, Name = "reports.quickaccountstatement", DisplayName = "اختصار كشف الحساب المباشر", Category = "التقارير", CreatedAt = createdAt },
                new Permission { Id = 72, Name = "reports.accountstatement", DisplayName = "عرض كشف الحساب من السجلات", Category = "التقارير", CreatedAt = createdAt },

                new Permission { Id = 24, Name = "dashboard.view", DisplayName = "عرض لوحة التحكم", Category = "لوحة التحكم", CreatedAt = createdAt },
                new Permission { Id = 111, Name = "dashboard.companyperformance", DisplayName = "عرض أداء الشركة", Category = "لوحة التحكم", CreatedAt = createdAt },
                new Permission { Id = 25, Name = "dashboard.widget.stats", DisplayName = "عرض إحصائيات لوحة التحكم", Category = "لوحة التحكم", CreatedAt = createdAt },
                new Permission { Id = 26, Name = "dashboard.widget.accounts", DisplayName = "عرض أرصدة الحسابات بلوحة التحكم", Category = "لوحة التحكم", CreatedAt = createdAt },
                new Permission { Id = 27, Name = "dashboard.widget.links", DisplayName = "عرض الروابط السريعة بلوحة التحكم", Category = "لوحة التحكم", CreatedAt = createdAt },
                new Permission { Id = 112, Name = "dashboard.widget.search", DisplayName = "عرض البحث السريع بلوحة التحكم", Category = "لوحة التحكم", CreatedAt = createdAt },
                new Permission { Id = 110, Name = "dashboard.widget.cashboxes", DisplayName = "عرض أرصدة الصناديق بلوحة التحكم", Category = "لوحة التحكم", CreatedAt = createdAt },
                new Permission { Id = 28, Name = "expenses.view", DisplayName = "عرض المصاريف", Category = "المصاريف", CreatedAt = createdAt },
                new Permission { Id = 29, Name = "expenses.create", DisplayName = "إنشاء المصاريف", Category = "المصاريف", CreatedAt = createdAt },
                new Permission { Id = 30, Name = "expenses.edit", DisplayName = "تعديل المصاريف", Category = "المصاريف", CreatedAt = createdAt },
                new Permission { Id = 31, Name = "expenses.delete", DisplayName = "حذف المصاريف", Category = "المصاريف", CreatedAt = createdAt },
                new Permission { Id = 32, Name = "expenses.approve", DisplayName = "اعتماد المصاريف", Category = "المصاريف", CreatedAt = createdAt },
                new Permission { Id = 33, Name = "transfers.view", DisplayName = "عرض الحوالات", Category = "الحوالات", CreatedAt = createdAt },
                new Permission { Id = 34, Name = "transfers.create", DisplayName = "إنشاء الحوالات", Category = "الحوالات", CreatedAt = createdAt },
                new Permission { Id = 35, Name = "transfers.approve", DisplayName = "اعتماد الحوالات", Category = "الحوالات", CreatedAt = createdAt },
                new Permission { Id = 113, Name = "transfers.manage", DisplayName = "إدارة الحوالات", Category = "الحوالات", CreatedAt = createdAt },
                new Permission { Id = 36, Name = "currencies.view", DisplayName = "عرض العملات", Category = "العملات", CreatedAt = createdAt },
                new Permission { Id = 37, Name = "currencies.create", DisplayName = "إنشاء العملات", Category = "العملات", CreatedAt = createdAt },
                new Permission { Id = 38, Name = "currencies.edit", DisplayName = "تعديل العملات", Category = "العملات", CreatedAt = createdAt },
                new Permission { Id = 39, Name = "currencies.delete", DisplayName = "حذف العملات", Category = "العملات", CreatedAt = createdAt },
                new Permission { Id = 40, Name = "suppliers.view", DisplayName = "عرض الموردين", Category = "الموردين", CreatedAt = createdAt },
                new Permission { Id = 41, Name = "suppliers.create", DisplayName = "إنشاء الموردين", Category = "الموردين", CreatedAt = createdAt },
                new Permission { Id = 42, Name = "suppliers.edit", DisplayName = "تعديل الموردين", Category = "الموردين", CreatedAt = createdAt },
                new Permission { Id = 43, Name = "suppliers.delete", DisplayName = "حذف الموردين", Category = "الموردين", CreatedAt = createdAt },
                new Permission { Id = 44, Name = "systemsettings.view", DisplayName = "عرض إعدادات النظام", Category = "إعدادات النظام", CreatedAt = createdAt },
                new Permission { Id = 45, Name = "systemsettings.create", DisplayName = "إنشاء إعدادات النظام", Category = "إعدادات النظام", CreatedAt = createdAt },
                new Permission { Id = 46, Name = "systemsettings.edit", DisplayName = "تعديل إعدادات النظام", Category = "إعدادات النظام", CreatedAt = createdAt },
                new Permission { Id = 47, Name = "systemsettings.delete", DisplayName = "حذف إعدادات النظام", Category = "إعدادات النظام", CreatedAt = createdAt },
                new Permission { Id = 48, Name = "assets.view", DisplayName = "عرض الأصول", Category = "الأصول", CreatedAt = createdAt },
                new Permission { Id = 49, Name = "assets.create", DisplayName = "إنشاء الأصول", Category = "الأصول", CreatedAt = createdAt },
                new Permission { Id = 50, Name = "assets.edit", DisplayName = "تعديل الأصول", Category = "الأصول", CreatedAt = createdAt },
                new Permission { Id = 51, Name = "assets.delete", DisplayName = "حذف الأصول", Category = "الأصول", CreatedAt = createdAt },
                new Permission { Id = 52, Name = "assetexpenses.view", DisplayName = "عرض مصاريف الأصول", Category = "الأصول", CreatedAt = createdAt },
                new Permission { Id = 53, Name = "assetexpenses.create", DisplayName = "إنشاء مصروف أصل", Category = "الأصول", CreatedAt = createdAt },
                new Permission { Id = 114, Name = "assetexpenses.delete", DisplayName = "حذف مصروف أصل", Category = "الأصول", CreatedAt = createdAt },
                new Permission { Id = 54, Name = "reports.pending", DisplayName = "عرض الحركات غير المرحلة", Category = "التقارير", CreatedAt = createdAt },
                new Permission { Id = 55, Name = "reports.dynamic", DisplayName = "التقارير التفاعلية", Category = "التقارير", CreatedAt = createdAt },
                new Permission { Id = 56, Name = "permissiongroups.view", DisplayName = "عرض مجموعات الصلاحيات", Category = "الصلاحيات", CreatedAt = createdAt },
                new Permission { Id = 57, Name = "permissiongroups.create", DisplayName = "إنشاء مجموعة صلاحيات", Category = "الصلاحيات", CreatedAt = createdAt },
                new Permission { Id = 58, Name = "permissiongroups.edit", DisplayName = "تعديل مجموعة صلاحيات", Category = "الصلاحيات", CreatedAt = createdAt },
                new Permission { Id = 59, Name = "permissiongroups.delete", DisplayName = "حذف مجموعة صلاحيات", Category = "الصلاحيات", CreatedAt = createdAt },
                new Permission { Id = 60, Name = "paymentvouchers.approve", DisplayName = "اعتماد سندات الدفع", Category = "السندات", CreatedAt = createdAt },
                new Permission { Id = 61, Name = "workflowapprovals.view", DisplayName = "عرض موافقات سندات الدفع", Category = "سير العمل", CreatedAt = createdAt },
                new Permission { Id = 62, Name = "workflowapprovals.process", DisplayName = "معالجة موافقات سندات الدفع", Category = "سير العمل", CreatedAt = createdAt },
                new Permission { Id = 63, Name = "workflowdefinitions.manage", DisplayName = "إدارة سير عمل السندات", Category = "سير العمل", CreatedAt = createdAt },
                new Permission { Id = 64, Name = "notifications.view", DisplayName = "عرض الإشعارات", Category = "سير العمل", CreatedAt = createdAt },

                new Permission { Id = 65, Name = "agents.view", DisplayName = "عرض الوكلاء", Category = "الوكلاء", CreatedAt = createdAt },
                new Permission { Id = 66, Name = "agents.create", DisplayName = "إنشاء وكيل", Category = "الوكلاء", CreatedAt = createdAt },
                new Permission { Id = 67, Name = "agents.edit", DisplayName = "تعديل وكيل", Category = "الوكلاء", CreatedAt = createdAt },
                new Permission { Id = 68, Name = "agents.delete", DisplayName = "حذف وكيل", Category = "الوكلاء", CreatedAt = createdAt },
                new Permission { Id = 69, Name = "userbalances.view", DisplayName = "عرض أرصدة حسابات المستخدم", Category = "المستخدمين", CreatedAt = createdAt },
                new Permission { Id = 71, Name = "dynamicscreens.manage", DisplayName = "إدارة الشاشات الديناميكية", Category = "الشاشات الديناميكية", CreatedAt = createdAt },

                // Account management permissions
                new Permission { Id = 73, Name = "accountmanagement.businessstatementbulk", DisplayName = "كشف حساب العميل الكلي", Category = "إدارة الحسابات", CreatedAt = createdAt },
                new Permission { Id = 74, Name = "accountmanagement.busnissstatment", DisplayName = "كشف حساب العميل", Category = "إدارة الحسابات", CreatedAt = createdAt },
                new Permission { Id = 75, Name = "accountmanagement.driverpayment", DisplayName = "دفعات السائق", Category = "إدارة الحسابات", CreatedAt = createdAt },
                new Permission { Id = 76, Name = "accountmanagement.driverstatment", DisplayName = "كشف حساب السائق", Category = "إدارة الحسابات", CreatedAt = createdAt },
                new Permission { Id = 77, Name = "accountmanagement.userpayment", DisplayName = "دفعات العميل", Category = "إدارة الحسابات", CreatedAt = createdAt },
                new Permission { Id = 78, Name = "accountmanagement.busnissshipmentsreturn", DisplayName = "شحنات العميل المرتجعة", Category = "إدارة الحسابات", CreatedAt = createdAt },
                new Permission { Id = 79, Name = "accountmanagement.receivepayments", DisplayName = "استلام المدفوعات", Category = "إدارة الحسابات", CreatedAt = createdAt },
                new Permission { Id = 80, Name = "accountmanagement.receiveretpayments", DisplayName = "استلام مدفوعات المرتجعات", Category = "إدارة الحسابات", CreatedAt = createdAt },
                new Permission { Id = 81, Name = "accountmanagement.businessretstatementbulk", DisplayName = "كشف مرتجعات العميل الكلي", Category = "إدارة الحسابات", CreatedAt = createdAt },
                new Permission { Id = 82, Name = "accountmanagement.printslip", DisplayName = "طباعة سند الحساب", Category = "إدارة الحسابات", CreatedAt = createdAt },

                // Payroll and employees
                new Permission { Id = 83, Name = "payroll.view", DisplayName = "عرض الرواتب", Category = "الرواتب", CreatedAt = createdAt },
                new Permission { Id = 84, Name = "payroll.process", DisplayName = "معالجة الرواتب", Category = "الرواتب", CreatedAt = createdAt },
                new Permission { Id = 85, Name = "employees.view", DisplayName = "عرض الموظفين", Category = "الموظفين", CreatedAt = createdAt },
                new Permission { Id = 86, Name = "employees.create", DisplayName = "إنشاء موظف", Category = "الموظفين", CreatedAt = createdAt },
                new Permission { Id = 87, Name = "employees.edit", DisplayName = "تعديل موظف", Category = "الموظفين", CreatedAt = createdAt },
                new Permission { Id = 88, Name = "employees.delete", DisplayName = "حذف موظف", Category = "الموظفين", CreatedAt = createdAt },
                new Permission { Id = 89, Name = "employeeadvances.view", DisplayName = "عرض سندات صرف السلف", Category = "سلف الموظفين", CreatedAt = createdAt },
                new Permission { Id = 90, Name = "employeeadvances.create", DisplayName = "إنشاء سند صرف سلفة", Category = "سلف الموظفين", CreatedAt = createdAt },
                new Permission { Id = 91, Name = "salarypayments.view", DisplayName = "عرض سندات صرف الرواتب", Category = "الرواتب", CreatedAt = createdAt },
                new Permission { Id = 92, Name = "salarypayments.create", DisplayName = "إنشاء سند صرف راتب", Category = "الرواتب", CreatedAt = createdAt },

                // Voucher management
                new Permission { Id = 93, Name = "disbursementvouchers.view", DisplayName = "عرض سندات الدفع", Category = "السندات", CreatedAt = createdAt },
                new Permission { Id = 94, Name = "disbursementvouchers.create", DisplayName = "إنشاء سند دفع", Category = "السندات", CreatedAt = createdAt },
                new Permission { Id = 95, Name = "disbursementvouchers.delete", DisplayName = "حذف سند دفع", Category = "السندات", CreatedAt = createdAt },
                new Permission { Id = 96, Name = "receiptvouchers.view", DisplayName = "عرض سندات القبض", Category = "السندات", CreatedAt = createdAt },
                new Permission { Id = 97, Name = "receiptvouchers.create", DisplayName = "إنشاء سند قبض", Category = "السندات", CreatedAt = createdAt },
                new Permission { Id = 98, Name = "receiptvouchers.delete", DisplayName = "حذف سند قبض", Category = "السندات", CreatedAt = createdAt },
                new Permission { Id = 99, Name = "paymentvouchers.view", DisplayName = "عرض سندات الدفع", Category = "السندات", CreatedAt = createdAt },
                new Permission { Id = 100, Name = "paymentvouchers.create", DisplayName = "إنشاء سند دفع", Category = "السندات", CreatedAt = createdAt },
                new Permission { Id = 101, Name = "paymentvouchers.delete", DisplayName = "حذف سند دفع", Category = "السندات", CreatedAt = createdAt },

                // Cash box closures
                new Permission { Id = 102, Name = "cashclosures.view", DisplayName = "عرض إغلاقات الصندوق", Category = "إغلاق الصندوق", CreatedAt = createdAt },
                new Permission { Id = 103, Name = "cashclosures.create", DisplayName = "إنشاء إغلاق صندوق", Category = "إغلاق الصندوق", CreatedAt = createdAt },
                new Permission { Id = 104, Name = "cashclosures.approve", DisplayName = "اعتماد إغلاقات الصندوق", Category = "إغلاق الصندوق", CreatedAt = createdAt },
                new Permission { Id = 105, Name = "cashclosures.report", DisplayName = "تقرير إغلاقات الصندوق", Category = "إغلاق الصندوق", CreatedAt = createdAt },

                // Asset types
                new Permission { Id = 106, Name = "assettypes.view", DisplayName = "عرض أنواع الأصول", Category = "الأصول", CreatedAt = createdAt },
                new Permission { Id = 107, Name = "assettypes.create", DisplayName = "إنشاء نوع أصل", Category = "الأصول", CreatedAt = createdAt },
                new Permission { Id = 108, Name = "assettypes.edit", DisplayName = "تعديل نوع أصل", Category = "الأصول", CreatedAt = createdAt },
                new Permission { Id = 109, Name = "assettypes.delete", DisplayName = "حذف نوع أصل", Category = "الأصول", CreatedAt = createdAt }
            );
        }
    }
}

