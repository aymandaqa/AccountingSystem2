using System;
using System.Collections.Generic;

namespace AccountingSystem.ViewModels
{
    public class PaginatedListViewModel<T>
    {
        public required IReadOnlyList<T> Items { get; init; }

        public int TotalCount { get; init; }

        public int PageIndex { get; init; }

        public int PageSize { get; init; }

        public string? SearchTerm { get; init; }

        public DateTime? FromDate { get; init; }

        public DateTime? ToDate { get; init; }

        public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    }
}

