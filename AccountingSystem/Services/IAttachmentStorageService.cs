using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace AccountingSystem.Services
{
    public interface IAttachmentStorageService
    {
        Task<AttachmentSaveResult?> SaveAsync(IFormFile? file, string category, string? existingPath = null, CancellationToken cancellationToken = default);

        void Delete(string? relativePath);
    }

    public sealed class AttachmentSaveResult
    {
        public string FileName { get; init; } = string.Empty;

        public string FilePath { get; init; } = string.Empty;
    }
}
