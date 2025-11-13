using System;
using System.IO;
using AccountingSystem.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AccountingSystem.Services
{
    public class AttachmentStorageService : IAttachmentStorageService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<AttachmentStorageService> _logger;

        public AttachmentStorageService(IWebHostEnvironment environment, ILogger<AttachmentStorageService> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public async Task<AttachmentSaveResult?> SaveAsync(IFormFile? file, string category, string? existingPath = null, CancellationToken cancellationToken = default)
        {
            if (file == null || file.Length == 0)
            {
                return null;
            }

            var uploadsRoot = EnsureUploadsDirectory(category);
            var extension = Path.GetExtension(file.FileName);
            var generatedFileName = $"{Guid.NewGuid():N}{extension}";
            var relativePath = Path.Combine("attachments", category, generatedFileName);
            var publicPath = AttachmentPathHelper.NormalizeForClient(relativePath);
            var absolutePath = Path.Combine(uploadsRoot, generatedFileName);

            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

            try
            {
                await using var stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await file.CopyToAsync(stream, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save attachment {FileName} to {Path}", file.FileName, absolutePath);
                throw;
            }

            if (!string.IsNullOrEmpty(existingPath))
            {
                var normalizedExistingPath = AttachmentPathHelper.NormalizeForClient(existingPath);
                if (!string.Equals(normalizedExistingPath, publicPath, StringComparison.OrdinalIgnoreCase))
                {
                    Delete(existingPath);
                }
            }

            return new AttachmentSaveResult
            {
                FileName = Path.GetFileName(file.FileName),
                FilePath = publicPath ?? string.Empty
            };
        }

        public void Delete(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return;
            }

            var trimmedPath = AttachmentPathHelper.NormalizeForFileSystem(relativePath);
            if (string.IsNullOrEmpty(trimmedPath))
            {
                return;
            }
            var root = GetWebRoot();
            var absolutePath = Path.Combine(root, trimmedPath.Replace('/', Path.DirectorySeparatorChar));

            try
            {
                if (File.Exists(absolutePath))
                {
                    File.Delete(absolutePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete attachment at {Path}", absolutePath);
            }
        }

        private string EnsureUploadsDirectory(string category)
        {
            var root = GetWebRoot();
            var uploadsRoot = Path.Combine(root, "attachments", category);
            Directory.CreateDirectory(uploadsRoot);
            return uploadsRoot;
        }

        private string GetWebRoot()
        {
            if (!string.IsNullOrEmpty(_environment.WebRootPath))
            {
                return _environment.WebRootPath;
            }

            var fallback = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }
}
