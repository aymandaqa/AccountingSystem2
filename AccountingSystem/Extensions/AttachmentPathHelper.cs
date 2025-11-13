using System;
using Microsoft.AspNetCore.Mvc;

namespace AccountingSystem.Extensions
{
    public static class AttachmentPathHelper
    {
        public static string? NormalizeForClient(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            var trimmed = path.Trim();

            if (trimmed.StartsWith("~", StringComparison.Ordinal))
            {
                trimmed = trimmed[1..];
            }

            trimmed = trimmed.TrimStart('/', '\\');

            if (string.IsNullOrEmpty(trimmed))
            {
                return null;
            }

            return "~/" + trimmed.Replace('\\', '/');
        }

        public static string NormalizeForFileSystem(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            var trimmed = path.Trim();

            if (trimmed.StartsWith("~/", StringComparison.Ordinal) || trimmed.StartsWith("~\\", StringComparison.Ordinal))
            {
                trimmed = trimmed[2..];
            }
            else if (trimmed.StartsWith("~", StringComparison.Ordinal))
            {
                trimmed = trimmed[1..];
            }

            trimmed = trimmed.TrimStart('/', '\\');

            return trimmed.Replace('\\', '/');
        }

        public static string ContentFromAttachmentPath(this IUrlHelper urlHelper, string? path)
        {
            if (urlHelper == null)
            {
                throw new ArgumentNullException(nameof(urlHelper));
            }

            var normalized = NormalizeForClient(path);
            return string.IsNullOrEmpty(normalized) ? string.Empty : urlHelper.Content(normalized);
        }
    }
}
