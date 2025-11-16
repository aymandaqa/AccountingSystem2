using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AccountingSystem.ViewModels.Workflows;

namespace AccountingSystem.Services
{
    public interface IWorkflowApprovalViewModelFactory
    {
        Task<IReadOnlyList<WorkflowApprovalViewModel>> BuildPendingApprovalsAsync(string userId, CancellationToken cancellationToken = default);
    }
}
