using AccountingSystem.Models.Workflows;

namespace AccountingSystem.Services
{
    public interface IWorkflowService
    {
        Task<WorkflowDefinition?> GetActiveDefinitionAsync(WorkflowDocumentType documentType, int? branchId, CancellationToken cancellationToken = default);
        Task<WorkflowInstance?> StartWorkflowAsync(WorkflowDefinition definition, WorkflowDocumentType documentType, int documentId, string initiatorId, int? branchId, CancellationToken cancellationToken = default);
        Task<WorkflowAction?> GetWorkflowActionAsync(int id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<WorkflowAction>> GetPendingActionsForUserAsync(string userId, CancellationToken cancellationToken = default);
        Task ProcessActionAsync(int actionId, string userId, bool approve, string? notes, CancellationToken cancellationToken = default);
    }
}
