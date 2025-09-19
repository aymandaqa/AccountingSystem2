using System;
using System.Collections.Generic;

namespace AccountingSystem.ViewModels
{
    public class SlickGridRequest
    {
        private const int DefaultPageSize = 20;
        private const int MaxPageSize = 100;

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = DefaultPageSize;

        public string? SortField { get; set; }

        public string? SortOrder { get; set; }

        public string? Search { get; set; }

        public bool IsDescending => string.Equals(SortOrder, "desc", StringComparison.OrdinalIgnoreCase);

        public int GetValidatedPage() => Page < 1 ? 1 : Page;

        public int GetValidatedPageSize() => PageSize <= 0 ? DefaultPageSize : Math.Min(PageSize, MaxPageSize);
    }

    public class SlickGridResponse<T>
    {
        public int TotalCount { get; set; }

        public IEnumerable<T> Items { get; set; } = Array.Empty<T>();
    }
}
