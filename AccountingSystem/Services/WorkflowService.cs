using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Models.Workflows;
using AccountingSystem.Models.DynamicScreens;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AccountingSystem.Services
{
    public class WorkflowService : IWorkflowService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly IPaymentVoucherProcessor _paymentVoucherProcessor;
        private readonly IReceiptVoucherProcessor _receiptVoucherProcessor;
        private readonly IDisbursementVoucherProcessor _disbursementVoucherProcessor;
        private readonly IAssetExpenseProcessor _assetExpenseProcessor;

        public WorkflowService(
            ApplicationDbContext context,
            INotificationService notificationService,
            IPaymentVoucherProcessor paymentVoucherProcessor,
            IReceiptVoucherProcessor receiptVoucherProcessor,
            IDisbursementVoucherProcessor disbursementVoucherProcessor,
            IAssetExpenseProcessor assetExpenseProcessor)
        {
            _context = context;
            _notificationService = notificationService;
            _paymentVoucherProcessor = paymentVoucherProcessor;
            _receiptVoucherProcessor = receiptVoucherProcessor;
            _disbursementVoucherProcessor = disbursementVoucherProcessor;
            _assetExpenseProcessor = assetExpenseProcessor;
        }

        public async Task<WorkflowDefinition?> GetActiveDefinitionAsync(WorkflowDocumentType documentType, int? branchId, CancellationToken cancellationToken = default)
        {
            return await _context.WorkflowDefinitions
                .Include(d => d.Steps)
                .Where(d => d.DocumentType == documentType && d.IsActive)
                .OrderByDescending(d => d.BranchId.HasValue)
                .ThenByDescending(d => d.UpdatedAt ?? d.CreatedAt)
                .ThenByDescending(d => d.Id)
                .FirstOrDefaultAsync(d => !d.BranchId.HasValue || d.BranchId == branchId, cancellationToken);
        }

        public async Task<WorkflowInstance?> StartWorkflowAsync(
            WorkflowDefinition definition,
            WorkflowDocumentType documentType,
            int documentId,
            string initiatorId,
            int? branchId,
            decimal documentAmount,
            decimal documentAmountInBase,
            int? documentCurrencyId,
            CancellationToken cancellationToken = default)
        {
            await _context.Entry(definition).Collection(d => d.Steps).LoadAsync(cancellationToken);
            var applicableSteps = definition.Steps
                .Where(s => (!s.MinAmount.HasValue || documentAmountInBase >= s.MinAmount.Value)
                            && (!s.MaxAmount.HasValue || documentAmountInBase <= s.MaxAmount.Value))
                .OrderBy(s => s.Order)
                .ToList();

            if (!applicableSteps.Any())
            {
                applicableSteps = definition.Steps
                    .OrderBy(s => s.Order)
                    .ToList();

                if (!applicableSteps.Any())
                {
                    return null;
                }
            }

            var instance = new WorkflowInstance
            {
                WorkflowDefinitionId = definition.Id,
                DocumentType = documentType,
                DocumentId = documentId,
                DocumentAmount = documentAmount,
                DocumentAmountInBase = documentAmountInBase,
                DocumentCurrencyId = documentCurrencyId,
                Status = WorkflowInstanceStatus.InProgress,
                CurrentStepOrder = applicableSteps.First().Order,
                InitiatorId = initiatorId
            };

            await _context.WorkflowInstances.AddAsync(instance, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            var actions = new List<WorkflowAction>();
            foreach (var step in applicableSteps)
            {
                actions.Add(new WorkflowAction
                {
                    WorkflowInstanceId = instance.Id,
                    WorkflowStepId = step.Id,
                    Status = WorkflowActionStatus.Pending
                });
            }

            await _context.WorkflowActions.AddRangeAsync(actions, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            var firstStep = applicableSteps.First();
            var firstAction = actions.FirstOrDefault(a => a.WorkflowStepId == firstStep.Id);
            if (firstAction != null)
            {
                await NotifyApproversAsync(instance, firstStep, firstAction, branchId, cancellationToken);
            }

            return instance;
        }

        public async Task<WorkflowAction?> GetWorkflowActionAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _context.WorkflowActions
                .Include(a => a.WorkflowStep)
                .Include(a => a.WorkflowInstance)
                .ThenInclude(i => i.WorkflowDefinition)
                .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<WorkflowAction>> GetPendingActionsForUserAsync(string userId, CancellationToken cancellationToken = default)
        {
            var user = await _context.Users
                .Include(u => u.UserBranches)
                .Include(u => u.UserPermissions).ThenInclude(up => up.Permission)
                .Include(u => u.UserPermissionGroups).ThenInclude(upg => upg.PermissionGroup).ThenInclude(pg => pg.PermissionGroupPermissions).ThenInclude(pgp => pgp.Permission)
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

            if (user == null)
                return Array.Empty<WorkflowAction>();

            var actions = await _context.WorkflowActions
                .Include(a => a.WorkflowStep)
                .Include(a => a.WorkflowInstance).ThenInclude(i => i.WorkflowDefinition)
                .Include(a => a.WorkflowInstance).ThenInclude(i => i.Initiator)
                .Include(a => a.WorkflowInstance).ThenInclude(i => i.DocumentCurrency)
                .Where(a => a.Status == WorkflowActionStatus.Pending && a.WorkflowInstance.Status == WorkflowInstanceStatus.InProgress)
                .ToListAsync(cancellationToken);

            var branchCache = new Dictionary<int, int?>();
            var eligible = new List<WorkflowAction>();

            foreach (var action in actions)
            {
                if (await IsUserEligibleForStepAsync(action, user, branchCache, cancellationToken))
                {
                    eligible.Add(action);
                }
            }

            return eligible;
        }

        public async Task ProcessActionAsync(int actionId, string userId, bool approve, string? notes, CancellationToken cancellationToken = default)
        {
            var action = await _context.WorkflowActions
                .Include(a => a.WorkflowStep)
                .Include(a => a.WorkflowInstance)
                    .ThenInclude(i => i.WorkflowDefinition)
                        .ThenInclude(d => d.Steps)
                .Include(a => a.WorkflowInstance)
                    .ThenInclude(i => i.Actions)
                        .ThenInclude(a => a.WorkflowStep)
                .FirstOrDefaultAsync(a => a.Id == actionId, cancellationToken);

            if (action == null)
                throw new InvalidOperationException("خطوة الموافقة غير موجودة");

            if (action.Status != WorkflowActionStatus.Pending)
                throw new InvalidOperationException("تمت معالجة هذه الخطوة مسبقاً");

            var user = await _context.Users
                .Include(u => u.UserBranches)
                .Include(u => u.UserPermissions).ThenInclude(up => up.Permission)
                .Include(u => u.UserPermissionGroups).ThenInclude(upg => upg.PermissionGroup).ThenInclude(pg => pg.PermissionGroupPermissions).ThenInclude(pgp => pgp.Permission)
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

            if (user == null)
                throw new InvalidOperationException("المستخدم غير موجود");

            if (!await IsUserEligibleForStepAsync(action, user, null, cancellationToken))
                throw new InvalidOperationException("لا تملك صلاحية اعتماد هذه الخطوة");

            action.Status = approve ? WorkflowActionStatus.Approved : WorkflowActionStatus.Rejected;
            action.UserId = userId;
            action.ActionedAt = DateTime.Now;
            action.Notes = notes;

            await _notificationService.MarkWorkflowActionNotificationsAsReadAsync(action.Id, userId, cancellationToken);

            if (!approve)
            {
                await HandleRejectionAsync(action, cancellationToken);
                return;
            }

            var orderedActions = action.WorkflowInstance.Actions
                .OrderBy(a => a.WorkflowStep.Order)
                .ToList();
            var currentIndex = orderedActions.FindIndex(a => a.Id == action.Id);
            WorkflowAction? nextAction = null;
            for (var i = currentIndex + 1; i < orderedActions.Count; i++)
            {
                if (orderedActions[i].Status == WorkflowActionStatus.Pending)
                {
                    nextAction = orderedActions[i];
                    break;
                }
            }

            if (nextAction == null)
            {
                await CompleteWorkflowAsync(action.WorkflowInstance, userId, cancellationToken);
            }
            else
            {
                action.WorkflowInstance.CurrentStepOrder = nextAction.WorkflowStep.Order;
                var branchContext = await ResolveDocumentBranchIdAsync(action.WorkflowInstance, cancellationToken);
                await NotifyApproversAsync(action.WorkflowInstance, nextAction.WorkflowStep, nextAction, branchContext, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        private async Task<bool> IsUserEligibleForStepAsync(WorkflowAction action, User user, Dictionary<int, int?>? branchCache, CancellationToken cancellationToken)
        {
            var step = action.WorkflowStep;
            switch (step.StepType)
            {
                case WorkflowStepType.SpecificUser:
                    return step.ApproverUserId == user.Id;
                case WorkflowStepType.Permission:
                    if (string.IsNullOrWhiteSpace(step.RequiredPermission))
                        return false;
                    var permissions = GetUserPermissions(user);
                    return permissions.Contains(step.RequiredPermission);
                case WorkflowStepType.Branch:
                    var branchId = step.BranchId;
                    if (!branchId.HasValue)
                    {
                        if (branchCache != null && branchCache.TryGetValue(action.WorkflowInstanceId, out var cachedBranchId))
                        {
                            branchId = cachedBranchId;
                        }
                        else
                        {
                            branchId = await ResolveDocumentBranchIdAsync(action.WorkflowInstance, cancellationToken);
                            if (branchCache != null)
                            {
                                branchCache[action.WorkflowInstanceId] = branchId;
                            }
                        }
                    }

                    if (!branchId.HasValue)
                        return false;

                    return user.UserBranches.Any(ub => ub.BranchId == branchId.Value);
                default:
                    return false;
            }
        }

        private HashSet<string> GetUserPermissions(User user)
        {
            var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var userPermission in user.UserPermissions.Where(p => p.IsGranted))
            {
                permissions.Add(userPermission.Permission.Name);
            }

            foreach (var group in user.UserPermissionGroups)
            {
                if (group.PermissionGroup?.PermissionGroupPermissions == null)
                    continue;

                foreach (var permission in group.PermissionGroup.PermissionGroupPermissions)
                {
                    permissions.Add(permission.Permission.Name);
                }
            }

            return permissions;
        }

        private async Task NotifyApproversAsync(WorkflowInstance instance, WorkflowStep step, WorkflowAction action, int? documentBranchId, CancellationToken cancellationToken)
        {
            var approvers = await ResolveApproverUserIdsAsync(step, documentBranchId, cancellationToken);
            if (!approvers.Any())
                throw new InvalidOperationException("لم يتم العثور على مستخدمين للخطوة المحددة");

            if (instance.DocumentCurrencyId.HasValue && instance.DocumentCurrency == null)
            {
                instance.DocumentCurrency = await _context.Currencies.FirstOrDefaultAsync(c => c.Id == instance.DocumentCurrencyId, cancellationToken);
            }

            var message = GetNotificationMessage(instance);

            var notifications = approvers.Select(userId => new Notification
            {
                UserId = userId,
                Title = GetNotificationTitle(instance.DocumentType),
                Message = message,
                Icon = "fa-diagram-project",
                Link = "/WorkflowApprovals",
                WorkflowActionId = action.Id,
                CreatedAt = DateTime.Now
            }).ToList();

            await _notificationService.CreateNotificationsAsync(notifications, cancellationToken);
        }

        private string GetNotificationTitle(WorkflowDocumentType documentType)
        {
            return documentType switch
            {
                WorkflowDocumentType.PaymentVoucher => "طلب موافقة سند صرف",
                WorkflowDocumentType.ReceiptVoucher => "طلب موافقة سند قبض",
                WorkflowDocumentType.DisbursementVoucher => "طلب موافقة سند دفع",
                WorkflowDocumentType.DynamicScreenEntry => "طلب موافقة حركة ديناميكية",
                WorkflowDocumentType.AssetExpense => "طلب موافقة مصروف أصل",
                _ => "طلب موافقة"
            };
        }

        private string GetNotificationMessage(WorkflowInstance instance)
        {
            var amountSuffix = GetAmountDescription(instance);
            return instance.DocumentType switch
            {
                WorkflowDocumentType.PaymentVoucher => $"يوجد سند صرف رقم {instance.DocumentId} بانتظار الموافقة{amountSuffix}",
                WorkflowDocumentType.ReceiptVoucher => $"يوجد سند قبض رقم {instance.DocumentId} بانتظار الموافقة{amountSuffix}",
                WorkflowDocumentType.DisbursementVoucher => $"يوجد سند دفع رقم {instance.DocumentId} بانتظار الموافقة{amountSuffix}",
                WorkflowDocumentType.DynamicScreenEntry => $"يوجد طلب على شاشة ديناميكية رقم {instance.DocumentId} بانتظار الموافقة{amountSuffix}",
                WorkflowDocumentType.AssetExpense => $"يوجد مصروف أصل رقم {instance.DocumentId} بانتظار الموافقة{amountSuffix}",
                _ => $"هناك مستند رقم {instance.DocumentId} بانتظار الموافقة{amountSuffix}"
            };
        }

        private string GetAmountDescription(WorkflowInstance instance)
        {
            var parts = new List<string>();

            if (instance.DocumentAmount != 0)
            {
                var currencyCode = instance.DocumentCurrency?.Code;
                parts.Add(!string.IsNullOrWhiteSpace(currencyCode)
                    ? $"{instance.DocumentAmount:N2} {currencyCode}"
                    : instance.DocumentAmount.ToString("N2"));
            }

            if (instance.DocumentAmountInBase != 0)
            {
                parts.Add($"ما يعادل {instance.DocumentAmountInBase:N2} بالعملة الأساسية");
            }

            return parts.Count > 0 ? $" ({string.Join(" - ", parts)})" : string.Empty;
        }

        private async Task<IEnumerable<string>> ResolveApproverUserIdsAsync(WorkflowStep step, int? documentBranchId, CancellationToken cancellationToken)
        {
            var result = new HashSet<string>();
            switch (step.StepType)
            {
                case WorkflowStepType.SpecificUser:
                    if (!string.IsNullOrWhiteSpace(step.ApproverUserId))
                        result.Add(step.ApproverUserId);
                    break;
                case WorkflowStepType.Permission:
                    if (!string.IsNullOrWhiteSpace(step.RequiredPermission))
                    {
                        var directUsers = await _context.UserPermissions
                            .Where(up => up.IsGranted && up.Permission.Name == step.RequiredPermission)
                            .Select(up => up.UserId)
                            .ToListAsync(cancellationToken);
                        foreach (var userId in directUsers)
                            result.Add(userId);

                        var groupUsers = await _context.UserPermissionGroups
                            .Where(upg => upg.PermissionGroup.PermissionGroupPermissions.Any(pgp => pgp.Permission.Name == step.RequiredPermission))
                            .Select(upg => upg.UserId)
                            .ToListAsync(cancellationToken);
                        foreach (var userId in groupUsers)
                            result.Add(userId);
                    }
                    break;
                case WorkflowStepType.Branch:
                    var branchId = step.BranchId ?? documentBranchId;
                    if (branchId.HasValue)
                    {
                        var branchUsers = await _context.UserBranches
                            .Where(ub => ub.BranchId == branchId.Value)
                            .Select(ub => ub.UserId)
                            .ToListAsync(cancellationToken);
                        foreach (var userId in branchUsers)
                            result.Add(userId);
                    }
                    break;
            }

            return result;
        }

        private async Task HandleRejectionAsync(WorkflowAction action, CancellationToken cancellationToken)
        {
            action.WorkflowInstance.Status = WorkflowInstanceStatus.Rejected;
            action.WorkflowInstance.CompletedAt = DateTime.Now;

            var pendingActions = await _context.WorkflowActions
                .Where(a => a.WorkflowInstanceId == action.WorkflowInstanceId && a.Status == WorkflowActionStatus.Pending)
                .ToListAsync(cancellationToken);

            foreach (var pending in pendingActions)
            {
                pending.Status = WorkflowActionStatus.Skipped;
            }

            switch (action.WorkflowInstance.DocumentType)
            {
                case WorkflowDocumentType.PaymentVoucher:
                    var voucher = await _context.PaymentVouchers.FirstOrDefaultAsync(v => v.Id == action.WorkflowInstance.DocumentId, cancellationToken);
                    if (voucher != null)
                    {
                        voucher.Status = PaymentVoucherStatus.Rejected;
                        voucher.ApprovedAt = null;
                        voucher.ApprovedById = null;
                        voucher.WorkflowInstanceId = action.WorkflowInstance.Id;
                    }
                    break;
                case WorkflowDocumentType.ReceiptVoucher:
                    var receipt = await _context.ReceiptVouchers.FirstOrDefaultAsync(v => v.Id == action.WorkflowInstance.DocumentId, cancellationToken);
                    if (receipt != null)
                    {
                        receipt.Status = ReceiptVoucherStatus.Rejected;
                        receipt.ApprovedAt = null;
                        receipt.ApprovedById = null;
                        receipt.WorkflowInstanceId = action.WorkflowInstance.Id;
                    }
                    break;
                case WorkflowDocumentType.DisbursementVoucher:
                    var disbursement = await _context.DisbursementVouchers.FirstOrDefaultAsync(v => v.Id == action.WorkflowInstance.DocumentId, cancellationToken);
                    if (disbursement != null)
                    {
                        disbursement.Status = DisbursementVoucherStatus.Rejected;
                        disbursement.ApprovedAt = null;
                        disbursement.ApprovedById = null;
                        disbursement.WorkflowInstanceId = action.WorkflowInstance.Id;
                    }
                    break;
                case WorkflowDocumentType.DynamicScreenEntry:
                    var entry = await _context.DynamicScreenEntries.FirstOrDefaultAsync(e => e.Id == action.WorkflowInstance.DocumentId, cancellationToken);
                    if (entry != null)
                    {
                        entry.Status = DynamicScreenEntryStatus.Rejected;
                        entry.RejectedAt = DateTime.Now;
                        entry.RejectedById = action.UserId;
                        entry.WorkflowInstanceId = action.WorkflowInstance.Id;
                    }
                    break;
                case WorkflowDocumentType.AssetExpense:
                {
                    var assetExpenseEntity = await _context.AssetExpenses
                        .FirstOrDefaultAsync(e => e.Id == action.WorkflowInstance.DocumentId, cancellationToken);
                    if (assetExpenseEntity != null)
                    {
                        assetExpenseEntity.WorkflowInstanceId = action.WorkflowInstance.Id;
                    }
                    break;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task CompleteWorkflowAsync(WorkflowInstance instance, string approvedById, CancellationToken cancellationToken)
        {
            instance.Status = WorkflowInstanceStatus.Approved;
            instance.CompletedAt = DateTime.Now;

            switch (instance.DocumentType)
            {
                case WorkflowDocumentType.PaymentVoucher:
                    var voucher = await _context.PaymentVouchers.FirstOrDefaultAsync(v => v.Id == instance.DocumentId, cancellationToken);
                    if (voucher != null)
                    {
                        voucher.WorkflowInstanceId = instance.Id;
                        await _paymentVoucherProcessor.FinalizeVoucherAsync(voucher, approvedById, cancellationToken);
                    }
                    break;
                case WorkflowDocumentType.ReceiptVoucher:
                    var receipt = await _context.ReceiptVouchers.FirstOrDefaultAsync(v => v.Id == instance.DocumentId, cancellationToken);
                    if (receipt != null)
                    {
                        receipt.WorkflowInstanceId = instance.Id;
                        await _receiptVoucherProcessor.FinalizeAsync(receipt, approvedById, cancellationToken);
                    }
                    break;
                case WorkflowDocumentType.DisbursementVoucher:
                    var disbursement = await _context.DisbursementVouchers.FirstOrDefaultAsync(v => v.Id == instance.DocumentId, cancellationToken);
                    if (disbursement != null)
                    {
                        disbursement.WorkflowInstanceId = instance.Id;
                        await _disbursementVoucherProcessor.FinalizeAsync(disbursement, approvedById, cancellationToken);
                    }
                    break;
                case WorkflowDocumentType.DynamicScreenEntry:
                    var entry = await _context.DynamicScreenEntries.FirstOrDefaultAsync(e => e.Id == instance.DocumentId, cancellationToken);
                    if (entry != null)
                    {
                        entry.WorkflowInstanceId = instance.Id;
                        entry.Status = DynamicScreenEntryStatus.Approved;
                        entry.ApprovedAt = DateTime.Now;
                        entry.ApprovedById = approvedById;
                    }
                    break;
                case WorkflowDocumentType.AssetExpense:
                {
                    var assetExpenseEntity = await _context.AssetExpenses.FirstOrDefaultAsync(e => e.Id == instance.DocumentId, cancellationToken);
                    if (assetExpenseEntity != null)
                    {
                        assetExpenseEntity.WorkflowInstanceId = instance.Id;
                        await _assetExpenseProcessor.FinalizeAsync(assetExpenseEntity, approvedById, cancellationToken);
                    }
                    break;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task<int?> ResolveDocumentBranchIdAsync(WorkflowInstance instance, CancellationToken cancellationToken)
        {
            return instance.DocumentType switch
            {
                WorkflowDocumentType.PaymentVoucher => await _context.PaymentVouchers
                    .Where(v => v.Id == instance.DocumentId)
                    .Select(v => v.CreatedBy.PaymentBranchId)
                    .FirstOrDefaultAsync(cancellationToken),
                WorkflowDocumentType.ReceiptVoucher => await _context.ReceiptVouchers
                    .Where(v => v.Id == instance.DocumentId)
                    .Select(v => v.CreatedBy.PaymentBranchId)
                    .FirstOrDefaultAsync(cancellationToken),
                WorkflowDocumentType.DisbursementVoucher => await _context.DisbursementVouchers
                    .Where(v => v.Id == instance.DocumentId)
                    .Select(v => v.CreatedBy.PaymentBranchId)
                    .FirstOrDefaultAsync(cancellationToken),
                WorkflowDocumentType.DynamicScreenEntry => await _context.DynamicScreenEntries
                    .Where(e => e.Id == instance.DocumentId)
                    .Select(e => e.BranchId)
                    .FirstOrDefaultAsync(cancellationToken),
                WorkflowDocumentType.AssetExpense => await _context.AssetExpenses
                    .Where(e => e.Id == instance.DocumentId)
                    .Select(e => e.Asset.BranchId)
                    .FirstOrDefaultAsync(cancellationToken),
                _ => null
            };
        }
    }
}
