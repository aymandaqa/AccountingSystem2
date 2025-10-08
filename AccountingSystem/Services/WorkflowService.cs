using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Models.Workflows;
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

        public WorkflowService(
            ApplicationDbContext context,
            INotificationService notificationService,
            IPaymentVoucherProcessor paymentVoucherProcessor)
        {
            _context = context;
            _notificationService = notificationService;
            _paymentVoucherProcessor = paymentVoucherProcessor;
        }

        public async Task<WorkflowDefinition?> GetActiveDefinitionAsync(WorkflowDocumentType documentType, int? branchId, CancellationToken cancellationToken = default)
        {
            return await _context.WorkflowDefinitions
                .Include(d => d.Steps)
                .Where(d => d.DocumentType == documentType && d.IsActive)
                .OrderByDescending(d => d.BranchId.HasValue)
                .ThenBy(d => d.Id)
                .FirstOrDefaultAsync(d => !d.BranchId.HasValue || d.BranchId == branchId, cancellationToken);
        }

        public async Task<WorkflowInstance?> StartWorkflowAsync(WorkflowDefinition definition, WorkflowDocumentType documentType, int documentId, string initiatorId, int? branchId, CancellationToken cancellationToken = default)
        {
            await _context.Entry(definition).Collection(d => d.Steps).LoadAsync(cancellationToken);
            var orderedSteps = definition.Steps.OrderBy(s => s.Order).ToList();

            if (!orderedSteps.Any())
            {
                return null;
            }

            var instance = new WorkflowInstance
            {
                WorkflowDefinitionId = definition.Id,
                DocumentType = documentType,
                DocumentId = documentId,
                Status = WorkflowInstanceStatus.InProgress,
                CurrentStepOrder = orderedSteps.First().Order,
                InitiatorId = initiatorId
            };

            await _context.WorkflowInstances.AddAsync(instance, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            var actions = new List<WorkflowAction>();
            foreach (var step in orderedSteps)
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

            var firstAction = actions.FirstOrDefault(a => a.WorkflowStepId == orderedSteps.First().Id);
            if (firstAction != null)
            {
                await NotifyApproversAsync(instance, orderedSteps.First(), firstAction, branchId, cancellationToken);
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
                .Where(a => a.Status == WorkflowActionStatus.Pending && a.WorkflowInstance.Status == WorkflowInstanceStatus.InProgress)
                .ToListAsync(cancellationToken);

            var eligible = actions.Where(a => IsUserEligibleForStep(a.WorkflowStep, user)).ToList();

            return eligible;
        }

        public async Task ProcessActionAsync(int actionId, string userId, bool approve, string? notes, CancellationToken cancellationToken = default)
        {
            var action = await _context.WorkflowActions
                .Include(a => a.WorkflowStep)
                .Include(a => a.WorkflowInstance)
                    .ThenInclude(i => i.WorkflowDefinition)
                        .ThenInclude(d => d.Steps)
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

            if (!IsUserEligibleForStep(action.WorkflowStep, user))
                throw new InvalidOperationException("لا تملك صلاحية اعتماد هذه الخطوة");

            action.Status = approve ? WorkflowActionStatus.Approved : WorkflowActionStatus.Rejected;
            action.UserId = userId;
            action.ActionedAt = DateTime.UtcNow;
            action.Notes = notes;

            await _notificationService.MarkWorkflowActionNotificationsAsReadAsync(action.Id, userId, cancellationToken);

            if (!approve)
            {
                await HandleRejectionAsync(action, cancellationToken);
                return;
            }

            var orderedSteps = action.WorkflowInstance.WorkflowDefinition.Steps.OrderBy(s => s.Order).ToList();
            var currentIndex = orderedSteps.FindIndex(s => s.Id == action.WorkflowStepId);
            var nextStep = currentIndex >= 0 && currentIndex + 1 < orderedSteps.Count
                ? orderedSteps[currentIndex + 1]
                : null;

            if (nextStep == null)
            {
                await CompleteWorkflowAsync(action.WorkflowInstance, userId, cancellationToken);
            }
            else
            {
                action.WorkflowInstance.CurrentStepOrder = nextStep.Order;
                var nextAction = await _context.WorkflowActions
                    .FirstOrDefaultAsync(a => a.WorkflowInstanceId == action.WorkflowInstanceId && a.WorkflowStepId == nextStep.Id, cancellationToken);
                if (nextAction != null)
                {
                    var branchContext = await ResolveDocumentBranchIdAsync(action.WorkflowInstance, cancellationToken);
                    await NotifyApproversAsync(action.WorkflowInstance, nextStep, nextAction, branchContext, cancellationToken);
                }

                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        private bool IsUserEligibleForStep(WorkflowStep step, User user)
        {
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
                    if (!step.BranchId.HasValue)
                        return false;
                    return user.UserBranches.Any(ub => ub.BranchId == step.BranchId.Value);
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

            var notifications = approvers.Select(userId => new Notification
            {
                UserId = userId,
                Title = GetNotificationTitle(instance.DocumentType),
                Message = GetNotificationMessage(instance),
                Link = "/WorkflowApprovals",
                WorkflowActionId = action.Id,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            await _notificationService.CreateNotificationsAsync(notifications, cancellationToken);
        }

        private string GetNotificationTitle(WorkflowDocumentType documentType)
        {
            return documentType switch
            {
                WorkflowDocumentType.PaymentVoucher => "طلب موافقة سند دفع",
                _ => "طلب موافقة"
            };
        }

        private string GetNotificationMessage(WorkflowInstance instance)
        {
            return instance.DocumentType switch
            {
                WorkflowDocumentType.PaymentVoucher => $"يوجد سند دفع رقم {instance.DocumentId} بانتظار الموافقة",
                _ => $"هناك مستند رقم {instance.DocumentId} بانتظار الموافقة"
            };
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
            action.WorkflowInstance.CompletedAt = DateTime.UtcNow;

            var pendingActions = await _context.WorkflowActions
                .Where(a => a.WorkflowInstanceId == action.WorkflowInstanceId && a.Status == WorkflowActionStatus.Pending)
                .ToListAsync(cancellationToken);

            foreach (var pending in pendingActions)
            {
                pending.Status = WorkflowActionStatus.Skipped;
            }

            if (action.WorkflowInstance.DocumentType == WorkflowDocumentType.PaymentVoucher)
            {
                var voucher = await _context.PaymentVouchers.FirstOrDefaultAsync(v => v.Id == action.WorkflowInstance.DocumentId, cancellationToken);
                if (voucher != null)
                {
                    voucher.Status = PaymentVoucherStatus.Rejected;
                    voucher.ApprovedAt = null;
                    voucher.ApprovedById = null;
                    voucher.WorkflowInstanceId = action.WorkflowInstance.Id;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task CompleteWorkflowAsync(WorkflowInstance instance, string approvedById, CancellationToken cancellationToken)
        {
            instance.Status = WorkflowInstanceStatus.Approved;
            instance.CompletedAt = DateTime.UtcNow;

            if (instance.DocumentType == WorkflowDocumentType.PaymentVoucher)
            {
                var voucher = await _context.PaymentVouchers.FirstOrDefaultAsync(v => v.Id == instance.DocumentId, cancellationToken);
                if (voucher != null)
                {
                    voucher.WorkflowInstanceId = instance.Id;
                    await _paymentVoucherProcessor.FinalizeVoucherAsync(voucher, approvedById, cancellationToken);
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
                _ => null
            };
        }
    }
}
