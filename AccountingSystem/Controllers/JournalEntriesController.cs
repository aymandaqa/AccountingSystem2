using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;
using AccountingSystem.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using Syncfusion.EJ2.Base;
using System.Text.Json;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "journal.view")]
    public class JournalEntriesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IJournalEntryService _journalEntryService;

        public JournalEntriesController(ApplicationDbContext context, IJournalEntryService journalEntryService)
        {
            _context = context;
            _journalEntryService = journalEntryService;
        }

        private sealed class JournalEntryFilterParameters
        {
            public DateTime? FromDate { get; set; }
            public DateTime? ToDate { get; set; }
            public int? BranchId { get; set; }
            public JournalEntryStatus? Status { get; set; }
            public string? SearchTerm { get; set; }
            public bool ShowUnbalancedOnly { get; set; }
        }

        private JournalEntryFilterParameters ReadFilterParameters(HttpRequest request, DataManagerRequest? dm = null)
        {
            static string? TryReadFromDictionary(object? dictionary, string key)
            {
                if (dictionary is IDictionary<string, object?> genericDict && genericDict.TryGetValue(key, out var value))
                {
                    return value?.ToString();
                }

                if (dictionary is IDictionary nonGenericDict && nonGenericDict.Contains(key))
                {
                    return nonGenericDict[key]?.ToString();
                }

                if (dictionary is JsonElement element && element.ValueKind == JsonValueKind.Object)
                {
                    if (element.TryGetProperty(key, out var property))
                    {
                        if (property.ValueKind == JsonValueKind.String)
                        {
                            return property.GetString();
                        }

                        if (property.ValueKind == JsonValueKind.Number)
                        {
                            return property.GetRawText();
                        }

                        if (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
                        {
                            return property.GetBoolean().ToString();
                        }

                        if (property.ValueKind == JsonValueKind.Null)
                        {
                            return null;
                        }

                        return property.GetRawText();
                    }
                }

                return null;
            }

            string? GetParamValue(string key)
            {
                if (request.Query.TryGetValue(key, out var queryValues))
                {
                    var queryValue = queryValues.ToString();
                    if (!string.IsNullOrWhiteSpace(queryValue))
                    {
                        return queryValue;
                    }
                }

                if (request.HasFormContentType && request.Form.TryGetValue(key, out var formValues))
                {
                    var formValue = formValues.ToString();
                    if (!string.IsNullOrWhiteSpace(formValue))
                    {
                        return formValue;
                    }
                }

                if (dm == null)
                {
                    return null;
                }

                var paramsProperty = dm.GetType().GetProperty("Params");
                if (paramsProperty != null)
                {
                    var value = TryReadFromDictionary(paramsProperty.GetValue(dm), key);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }

                var additionalParamsProperty = dm.GetType().GetProperty("AdditionalParams");
                if (additionalParamsProperty != null)
                {
                    var value = TryReadFromDictionary(additionalParamsProperty.GetValue(dm), key);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }

                return null;
            }

            var parameters = new JournalEntryFilterParameters();

            var fromValue = GetParamValue("fromDate");
            if (!string.IsNullOrWhiteSpace(fromValue) && DateTime.TryParse(fromValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fromDate))
            {
                parameters.FromDate = fromDate;
            }

            var toValue = GetParamValue("toDate");
            if (!string.IsNullOrWhiteSpace(toValue) && DateTime.TryParse(toValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var toDate))
            {
                parameters.ToDate = toDate;
            }

            var branchValue = GetParamValue("branchId");
            if (!string.IsNullOrWhiteSpace(branchValue) && int.TryParse(branchValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var branchId))
            {
                parameters.BranchId = branchId;
            }

            var statusValue = GetParamValue("status");
            if (!string.IsNullOrWhiteSpace(statusValue) && Enum.TryParse<JournalEntryStatus>(statusValue, out var status))
            {
                parameters.Status = status;
            }

            var showUnbalancedValue = GetParamValue("showUnbalancedOnly");
            if (!string.IsNullOrWhiteSpace(showUnbalancedValue) && bool.TryParse(showUnbalancedValue, out var showUnbalanced))
            {
                parameters.ShowUnbalancedOnly = showUnbalanced;
            }

            var searchValue = GetParamValue("searchTerm");
            if (!string.IsNullOrWhiteSpace(searchValue))
            {
                parameters.SearchTerm = searchValue.Trim();
            }

            return parameters;
        }

        private IQueryable<JournalEntry> ApplyFilters(IQueryable<JournalEntry> query, JournalEntryFilterParameters parameters)
        {
            if (parameters.BranchId.HasValue && parameters.BranchId.Value > 0)
            {
                query = query.Where(j => j.BranchId == parameters.BranchId.Value);
            }

            if (parameters.Status.HasValue)
            {
                query = query.Where(j => j.Status == parameters.Status.Value);
            }

            if (parameters.FromDate.HasValue)
            {
                var from = parameters.FromDate.Value.Date;
                query = query.Where(j => j.Date >= from);
            }

            if (parameters.ToDate.HasValue)
            {
                var to = parameters.ToDate.Value.Date.AddDays(1);
                query = query.Where(j => j.Date < to);
            }

            if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
            {
                var trimmedTerm = parameters.SearchTerm.Trim();
                var normalizedTerm = trimmedTerm.ToLowerInvariant();
                var likePattern = $"%{EscapeLikePattern(trimmedTerm)}%";
                var statusMatches = Enum.GetValues<JournalEntryStatus>()
                    .Where(s => GetStatusInfo(s).Text.Contains(trimmedTerm, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var searchForUnbalanced = normalizedTerm.Contains("غير") &&
                    (normalizedTerm.Contains("متوازن") || normalizedTerm.Contains("متزنة") || normalizedTerm.Contains("موزون"));

                decimal? numericSearch = null;
                if (decimal.TryParse(trimmedTerm, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariantNumeric))
                {
                    numericSearch = invariantNumeric;
                }
                else if (decimal.TryParse(trimmedTerm, NumberStyles.Number, CultureInfo.CurrentCulture, out var cultureNumeric))
                {
                    numericSearch = cultureNumeric;
                }

                query = query.Where(j =>
                    (j.Number != null && EF.Functions.Like(j.Number, likePattern)) ||
                    (j.Description != null && EF.Functions.Like(j.Description, likePattern)) ||
                    (j.Reference != null && EF.Functions.Like(j.Reference, likePattern)) ||
                    (j.Branch != null && j.Branch.NameAr != null && EF.Functions.Like(j.Branch.NameAr, likePattern)) ||
                    (j.CreatedBy != null && (
                        (j.CreatedBy.FirstName != null && EF.Functions.Like(j.CreatedBy.FirstName, likePattern)) ||
                        (j.CreatedBy.LastName != null && EF.Functions.Like(j.CreatedBy.LastName, likePattern)) ||
                        (j.CreatedBy.UserName != null && EF.Functions.Like(j.CreatedBy.UserName, likePattern))
                    )) ||
                    j.Lines.Any(l => l.Description != null && EF.Functions.Like(l.Description, likePattern)) ||
                    (statusMatches.Count > 0 && statusMatches.Contains(j.Status)) ||
                    (searchForUnbalanced && j.Lines.Sum(l => l.DebitAmount) != j.Lines.Sum(l => l.CreditAmount)) ||
                    (numericSearch.HasValue && (
                        j.TotalDebit == numericSearch.Value ||
                        j.TotalCredit == numericSearch.Value ||
                        j.Lines.Sum(l => l.DebitAmount) == numericSearch.Value ||
                        j.Lines.Sum(l => l.CreditAmount) == numericSearch.Value))
                );
            }

            if (parameters.ShowUnbalancedOnly)
            {
                query = query.Where(entry => entry.Lines.Sum(l => l.DebitAmount) != entry.Lines.Sum(l => l.CreditAmount));
            }

            return query;
        }

        private static string EscapeLikePattern(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("[", "[[]")
                .Replace("%", "[%]")
                .Replace("_", "[_]");
        }

        private IQueryable<JournalEntry> ApplyManagementFilters(IQueryable<JournalEntry> query, JournalEntryManagementFilters filters)
        {
            if (filters == null)
            {
                return query;
            }

            if (!string.IsNullOrWhiteSpace(filters.SearchTerm))
            {
                var normalized = filters.SearchTerm.Trim().ToLowerInvariant();
                query = query.Where(entry =>
                    (entry.Number != null && entry.Number.ToLower().Contains(normalized)) ||
                    (entry.Description != null && entry.Description.ToLower().Contains(normalized)) ||
                    (entry.Reference != null && entry.Reference.ToLower().Contains(normalized)) ||
                    (entry.Branch != null && entry.Branch.NameAr != null && entry.Branch.NameAr.ToLower().Contains(normalized)) ||
                    (entry.CreatedBy != null && (
                        (entry.CreatedBy.FirstName != null && entry.CreatedBy.FirstName.ToLower().Contains(normalized)) ||
                        (entry.CreatedBy.LastName != null && entry.CreatedBy.LastName.ToLower().Contains(normalized)) ||
                        (entry.CreatedBy.UserName != null && entry.CreatedBy.UserName.ToLower().Contains(normalized))
                    )));
            }

            if (!string.IsNullOrWhiteSpace(filters.Number))
            {
                var number = filters.Number.Trim();
                query = query.Where(entry => entry.Number != null && entry.Number.Contains(number));
            }

            if (!string.IsNullOrWhiteSpace(filters.Description))
            {
                var description = filters.Description.Trim();
                query = query.Where(entry => entry.Description != null && entry.Description.Contains(description));
            }

            if (!string.IsNullOrWhiteSpace(filters.Reference))
            {
                var reference = filters.Reference.Trim();
                query = query.Where(entry => entry.Reference != null && entry.Reference.Contains(reference));
            }

            if (filters.BranchId.HasValue && filters.BranchId.Value > 0)
            {
                query = query.Where(entry => entry.BranchId == filters.BranchId.Value);
            }

            if (!string.IsNullOrWhiteSpace(filters.Status) && Enum.TryParse<JournalEntryStatus>(filters.Status, true, out var status))
            {
                query = query.Where(entry => entry.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(filters.CreatedBy))
            {
                var createdBy = filters.CreatedBy.Trim().ToLowerInvariant();
                query = query.Where(entry => entry.CreatedBy != null && (
                    (entry.CreatedBy.FirstName != null && entry.CreatedBy.FirstName.ToLower().Contains(createdBy)) ||
                    (entry.CreatedBy.LastName != null && entry.CreatedBy.LastName.ToLower().Contains(createdBy)) ||
                    (entry.CreatedBy.UserName != null && entry.CreatedBy.UserName.ToLower().Contains(createdBy))));
            }

            if (filters.FromDate.HasValue)
            {
                var from = filters.FromDate.Value.Date;
                query = query.Where(entry => entry.Date >= from);
            }

            if (filters.ToDate.HasValue)
            {
                var to = filters.ToDate.Value.Date.AddDays(1);
                query = query.Where(entry => entry.Date < to);
            }

            if (filters.MinAmount.HasValue)
            {
                query = query.Where(entry => entry.TotalDebit >= filters.MinAmount.Value);
            }

            if (filters.MaxAmount.HasValue)
            {
                query = query.Where(entry => entry.TotalDebit <= filters.MaxAmount.Value);
            }

            if (filters.ShowUnbalancedOnly)
            {
                query = query.Where(entry => entry.TotalDebit != entry.TotalCredit);
            }

            return query;
        }

        private IQueryable<JournalEntry> ApplyManagementOrdering(IQueryable<JournalEntry> query, string? sortColumn, string? sortDirection)
        {
            var ascending = string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);

            return sortColumn?.ToLowerInvariant() switch
            {
                "number" => ascending ? query.OrderBy(entry => entry.Number) : query.OrderByDescending(entry => entry.Number),
                "branch" => ascending ? query.OrderBy(entry => entry.Branch.NameAr) : query.OrderByDescending(entry => entry.Branch.NameAr),
                "status" => ascending ? query.OrderBy(entry => entry.Status) : query.OrderByDescending(entry => entry.Status),
                "totaldebit" => ascending ? query.OrderBy(entry => entry.TotalDebit) : query.OrderByDescending(entry => entry.TotalDebit),
                "totalcredit" => ascending ? query.OrderBy(entry => entry.TotalCredit) : query.OrderByDescending(entry => entry.TotalCredit),
                "linescount" => ascending ? query.OrderBy(entry => entry.Lines.Count) : query.OrderByDescending(entry => entry.Lines.Count),
                "createdby" => ascending ? query.OrderBy(entry => entry.CreatedBy.FirstName).ThenBy(entry => entry.CreatedBy.LastName) : query.OrderByDescending(entry => entry.CreatedBy.FirstName).ThenByDescending(entry => entry.CreatedBy.LastName),
                "createdat" => ascending ? query.OrderBy(entry => entry.CreatedAt) : query.OrderByDescending(entry => entry.CreatedAt),
                "updatedat" => ascending ? query.OrderBy(entry => entry.UpdatedAt) : query.OrderByDescending(entry => entry.UpdatedAt),
                "reference" => ascending ? query.OrderBy(entry => entry.Reference) : query.OrderByDescending(entry => entry.Reference),
                "description" => ascending ? query.OrderBy(entry => entry.Description) : query.OrderByDescending(entry => entry.Description),
                _ => ascending
                    ? query.OrderBy(entry => entry.Date).ThenBy(entry => entry.Id)
                    : query.OrderByDescending(entry => entry.Date).ThenByDescending(entry => entry.Id)
            };
        }

        private static string BuildCreatedBy(string? firstName, string? lastName, string? userName)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(firstName))
            {
                parts.Add(firstName.Trim());
            }

            if (!string.IsNullOrWhiteSpace(lastName))
            {
                parts.Add(lastName.Trim());
            }

            if (parts.Count == 0 && !string.IsNullOrWhiteSpace(userName))
            {
                parts.Add(userName.Trim());
            }

            return parts.Count > 0 ? string.Join(' ', parts) : string.Empty;
        }

        // GET: JournalEntries
        [Authorize(Policy = "journal.view")]
        public async Task<IActionResult> Index()
        {
            var branches = await _context.Branches
                .OrderBy(b => b.NameAr)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.NameAr
                })
                .ToListAsync();

            branches.Insert(0, new SelectListItem { Value = string.Empty, Text = "كل الفروع" });

            var statuses = Enum.GetValues<JournalEntryStatus>()
                .Select(status =>
                {
                    var info = GetStatusInfo(status);
                    return new SelectListItem
                    {
                        Value = status.ToString(),
                        Text = info.Text
                    };
                })
                .ToList();

            statuses.Insert(0, new SelectListItem { Value = string.Empty, Text = "كل الحالات" });

            var viewModel = new JournalEntriesIndexViewModel
            {
                Branches = branches,
                Statuses = statuses
            };

            return View(viewModel);
        }

        [Authorize(Policy = "journal.view")]
        public async Task<IActionResult> Management()
        {
            var branches = await _context.Branches
                .OrderBy(b => b.NameAr)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.NameAr
                })
                .ToListAsync();

            var statuses = Enum.GetValues<JournalEntryStatus>()
                .Select(status =>
                {
                    var info = GetStatusInfo(status);
                    return new SelectListItem
                    {
                        Value = status.ToString(),
                        Text = info.Text
                    };
                })
                .ToList();

            var viewModel = new JournalEntryManagementViewModel
            {
                Branches = branches,
                Statuses = statuses
            };

            return View(viewModel);
        }

        [HttpGet]
        [Authorize(Policy = "journal.view")]
        public async Task<IActionResult> GetEntries()
        {
            int.TryParse(Request.Query["draw"], out var draw);
            int.TryParse(Request.Query["start"], out var start);
            int.TryParse(Request.Query["length"], out var length);

            var totalRecords = await _context.JournalEntries.CountAsync();

            var query = _context.JournalEntries
                .AsNoTracking()
                .Include(j => j.Branch)
                .Include(j => j.CreatedBy)
                .Include(j => j.Lines)
                .AsQueryable();

            var searchValue = Request.Query["search[value]"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(searchValue))
            {
                var normalizedSearch = searchValue.Trim().ToLower();
                query = query.Where(j =>
                    (j.Number != null && j.Number.ToLower().Contains(normalizedSearch)) ||
                    (j.Description != null && j.Description.ToLower().Contains(normalizedSearch)) ||
                    (j.Reference != null && j.Reference.ToLower().Contains(normalizedSearch)) ||
                    (j.Branch != null && j.Branch.NameAr != null && j.Branch.NameAr.ToLower().Contains(normalizedSearch)) ||
                    (j.CreatedBy != null && (
                        (j.CreatedBy.FirstName != null && j.CreatedBy.FirstName.ToLower().Contains(normalizedSearch)) ||
                        (j.CreatedBy.LastName != null && j.CreatedBy.LastName.ToLower().Contains(normalizedSearch)) ||
                        (j.CreatedBy.UserName != null && j.CreatedBy.UserName.ToLower().Contains(normalizedSearch))
                    )));
            }

            if (int.TryParse(Request.Query["branchId"], out var branchId))
            {
                query = query.Where(j => j.BranchId == branchId);
            }

            var statusFilter = Request.Query["status"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(statusFilter) && Enum.TryParse<JournalEntryStatus>(statusFilter, out var statusValue))
            {
                query = query.Where(j => j.Status == statusValue);
            }

            if (DateTime.TryParse(Request.Query["fromDate"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var fromDate))
            {
                var from = fromDate.Date;
                query = query.Where(j => j.Date >= from);
            }

            if (DateTime.TryParse(Request.Query["toDate"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var toDate))
            {
                var to = toDate.Date.AddDays(1);
                query = query.Where(j => j.Date < to);
            }

            var numberFilter = GetColumnSearchValue(0);
            if (!string.IsNullOrWhiteSpace(numberFilter))
            {
                query = query.Where(j => j.Number != null && j.Number.Contains(numberFilter));
            }

            var descriptionFilter = GetColumnSearchValue(2);
            if (!string.IsNullOrWhiteSpace(descriptionFilter))
            {
                query = query.Where(j => j.Description != null && j.Description.Contains(descriptionFilter));
            }

            var referenceFilter = GetColumnSearchValue(3);
            if (!string.IsNullOrWhiteSpace(referenceFilter))
            {
                query = query.Where(j => j.Reference != null && j.Reference.Contains(referenceFilter));
            }

            var branchFilterValue = GetColumnSearchValue(4);
            if (!string.IsNullOrWhiteSpace(branchFilterValue))
            {
                query = query.Where(j => j.Branch != null && j.Branch.NameAr != null && j.Branch.NameAr.Contains(branchFilterValue));
            }

            var createdByFilterValue = GetColumnSearchValue(5);
            if (!string.IsNullOrWhiteSpace(createdByFilterValue))
            {
                var normalizedCreatedBy = createdByFilterValue.Trim().ToLower();
                query = query.Where(j => j.CreatedBy != null &&
                    ((j.CreatedBy.FirstName != null && j.CreatedBy.FirstName.ToLower().Contains(normalizedCreatedBy)) ||
                     (j.CreatedBy.LastName != null && j.CreatedBy.LastName.ToLower().Contains(normalizedCreatedBy)) ||
                     (j.CreatedBy.UserName != null && j.CreatedBy.UserName.ToLower().Contains(normalizedCreatedBy))));
            }

            var dateColumnValue = Request.Query[$"columns[1][search][value]"].ToString();
            if (!string.IsNullOrWhiteSpace(dateColumnValue) && DateTime.TryParse(dateColumnValue, out var columnDate))
            {
                var dateOnly = columnDate.Date;
                query = query.Where(j => j.Date.Date == dateOnly);
            }

            var amountColumnValue = Request.Query[$"columns[6][search][value]"].ToString();
            if (!string.IsNullOrWhiteSpace(amountColumnValue) && decimal.TryParse(amountColumnValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var amountFilter))
            {
                query = query.Where(j => j.Lines.Sum(l => l.DebitAmount) == amountFilter);
            }

            var linesCountValue = Request.Query[$"columns[7][search][value]"].ToString();
            if (!string.IsNullOrWhiteSpace(linesCountValue) && int.TryParse(linesCountValue, out var linesFilter))
            {
                query = query.Where(j => j.Lines.Count == linesFilter);
            }

            var statusColumnValue = Request.Query[$"columns[8][search][value]"].ToString();
            if (!string.IsNullOrWhiteSpace(statusColumnValue) && Enum.TryParse<JournalEntryStatus>(statusColumnValue, out var statusColumn))
            {
                query = query.Where(j => j.Status == statusColumn);
            }

            var recordsFiltered = await query.CountAsync();

            if (length == -1)
            {
                length = recordsFiltered;
            }

            if (start > recordsFiltered)
            {
                start = 0;
            }

            var orderColumnIndexValue = Request.Query["order[0][column]"].FirstOrDefault();
            var orderDirection = Request.Query["order[0][dir]"].FirstOrDefault();
            if (!int.TryParse(orderColumnIndexValue, out var orderColumnIndex))
            {
                orderColumnIndex = 1;
            }

            query = ApplyOrdering(query, orderColumnIndex, orderDirection);

            var entries = await query
                .Skip(start)
                .Take(length)
                .ToListAsync();

            var data = entries.Select(entry =>
            {
                var totalAmount = entry.Lines.Sum(l => l.DebitAmount);
                var info = GetStatusInfo(entry.Status);
                var fullName = entry.CreatedBy != null
                    ? string.Join(' ', new[] { entry.CreatedBy.FirstName, entry.CreatedBy.LastName }
                        .Where(part => !string.IsNullOrWhiteSpace(part))).Trim()
                    : string.Empty;
                var createdByName = string.IsNullOrWhiteSpace(fullName)
                    ? entry.CreatedBy?.UserName ?? string.Empty
                    : fullName;
                return new JournalEntryViewModel
                {
                    Id = entry.Id,
                    Number = entry.Number,
                    Date = entry.Date,
                    DateFormatted = entry.Date.ToString("dd/MM/yyyy"),
                    DateGroup = entry.Date.ToString("yyyy-MM"),
                    Description = entry.Description,
                    Reference = entry.Reference ?? string.Empty,
                    Status = entry.Status.ToString(),
                    StatusDisplay = info.Text,
                    StatusClass = info.CssClass,
                    BranchName = entry.Branch.NameAr,
                    CreatedByName = createdByName,
                    TotalAmount = totalAmount,
                    TotalAmountFormatted = totalAmount.ToString("N2"),
                    LinesCount = entry.Lines.Count,
                    IsDraft = entry.Status == JournalEntryStatus.Draft,
                    CanDelete = entry.Status == JournalEntryStatus.Draft || entry.Status == JournalEntryStatus.Posted
                };
            }).ToList();

            return Json(new
            {
                draw,
                recordsTotal = totalRecords,
                recordsFiltered,
                data
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "journal.view")]
        public async Task<IActionResult> ManagementData([FromBody] JournalEntryManagementRequest request)
        {
            if (request == null)
            {
                return BadRequest();
            }

            var page = request.Page < 1 ? 1 : request.Page;
            var pageSize = request.PageSize;
            if (pageSize <= 0)
            {
                pageSize = 50;
            }
            pageSize = Math.Clamp(pageSize, 10, 500);

            var filters = request.Filters ?? new JournalEntryManagementFilters();

            var query = _context.JournalEntries
                .AsNoTracking()
                .Include(j => j.Branch)
                .Include(j => j.CreatedBy)
                .Include(j => j.Lines)
                .AsQueryable();

            query = ApplyManagementFilters(query, filters);

            var totalRecords = await query.CountAsync();

            var aggregates = await query
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    TotalDebit = g.Sum(entry => entry.TotalDebit),
                    TotalCredit = g.Sum(entry => entry.TotalCredit),
                    Balanced = g.Count(entry => entry.TotalDebit == entry.TotalCredit),
                    Unbalanced = g.Count(entry => entry.TotalDebit != entry.TotalCredit)
                })
                .FirstOrDefaultAsync();

            var orderedQuery = ApplyManagementOrdering(query, request.SortColumn, request.SortDirection);

            var totalPages = totalRecords == 0 ? 0 : (int)Math.Ceiling(totalRecords / (double)pageSize);
            if (totalPages == 0)
            {
                page = 1;
            }
            else if (page > totalPages)
            {
                page = totalPages;
            }

            var skip = (page - 1) * pageSize;
            if (skip < 0)
            {
                skip = 0;
            }

            var items = await orderedQuery
                .Skip(skip)
                .Take(pageSize)
                .Select(entry => new
                {
                    entry.Id,
                    entry.Number,
                    entry.Date,
                    entry.Description,
                    entry.Reference,
                    entry.Status,
                    BranchName = entry.Branch.NameAr,
                    CreatedByFirstName = entry.CreatedBy.FirstName,
                    CreatedByLastName = entry.CreatedBy.LastName,
                    CreatedByUserName = entry.CreatedBy.UserName,
                    entry.TotalDebit,
                    entry.TotalCredit,
                    LinesCount = entry.Lines.Count,
                    entry.CreatedAt,
                    entry.UpdatedAt
                })
                .ToListAsync();

            var result = new
            {
                page,
                pageSize,
                totalRecords,
                totalPages,
                summary = new
                {
                    totalDebit = aggregates?.TotalDebit ?? 0,
                    totalCredit = aggregates?.TotalCredit ?? 0,
                    balanced = aggregates?.Balanced ?? 0,
                    unbalanced = aggregates?.Unbalanced ?? 0
                },
                items = items.Select(entry =>
                {
                    var statusInfo = GetStatusInfo(entry.Status);
                    var createdByName = BuildCreatedBy(entry.CreatedByFirstName, entry.CreatedByLastName, entry.CreatedByUserName);
                    return new
                    {
                        entry.Id,
                        entry.Number,
                        Date = entry.Date,
                        DateFormatted = entry.Date.ToString("dd/MM/yyyy"),
                        MonthGroup = entry.Date.ToString("yyyy-MM"),
                        entry.Description,
                        entry.Reference,
                        Status = entry.Status.ToString(),
                        StatusDisplay = statusInfo.Text,
                        StatusClass = statusInfo.CssClass,
                        BranchName = entry.BranchName,
                        CreatedByName = createdByName,
                        entry.TotalDebit,
                        TotalDebitFormatted = entry.TotalDebit.ToString("N2"),
                        entry.TotalCredit,
                        TotalCreditFormatted = entry.TotalCredit.ToString("N2"),
                        entry.LinesCount,
                        IsDraft = entry.Status == JournalEntryStatus.Draft,
                        CanDelete = entry.Status == JournalEntryStatus.Draft || entry.Status == JournalEntryStatus.Posted,
                        IsBalanced = entry.TotalDebit == entry.TotalCredit,
                        CreatedAt = entry.CreatedAt,
                        CreatedAtFormatted = entry.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                        UpdatedAt = entry.UpdatedAt,
                        UpdatedAtFormatted = entry.UpdatedAt.HasValue ? entry.UpdatedAt.Value.ToString("dd/MM/yyyy HH:mm") : string.Empty
                    };
                })
            };

            return Json(result);
        }

        [HttpPost]
        [Authorize(Policy = "journal.view")]
        public IActionResult UrlDatasourceJournalEntries([FromBody] DataManagerRequest dm)
        {
            var filters = ReadFilterParameters(Request, dm);

            var query = _context.JournalEntries
                .AsNoTracking()
                .Include(j => j.Branch)
                .Include(j => j.CreatedBy)
                .Include(j => j.Lines)
                .AsQueryable();

            query = ApplyFilters(query, filters)
                .OrderByDescending(entry => entry.Date)
                .ThenByDescending(entry => entry.Id);

            var dataSource = query
                .Select(entry => new
                {
                    entry.Id,
                    entry.Number,
                    entry.Date,
                    entry.Description,
                    entry.Reference,
                    entry.Status,
                    BranchName = entry.Branch.NameAr,
                    CreatedByFirstName = entry.CreatedBy.FirstName,
                    CreatedByLastName = entry.CreatedBy.LastName,
                    CreatedByUserName = entry.CreatedBy.UserName,
                    TotalDebit = entry.Lines.Sum(l => l.DebitAmount),
                    TotalCredit = entry.Lines.Sum(l => l.CreditAmount),
                    LinesCount = entry.Lines.Count
                })
                .AsEnumerable()
                .Select(entry =>
                {
                    var info = GetStatusInfo(entry.Status);
                    var isBalanced = entry.TotalDebit == entry.TotalCredit;
                    var fullName = string.Join(' ', new[] { entry.CreatedByFirstName, entry.CreatedByLastName }
                        .Where(part => !string.IsNullOrWhiteSpace(part))).Trim();
                    var createdByName = string.IsNullOrWhiteSpace(fullName)
                        ? entry.CreatedByUserName ?? string.Empty
                        : fullName;
                    return new JournalEntryViewModel
                    {
                        Id = entry.Id,
                        Number = entry.Number,
                        Date = entry.Date,
                        Description = entry.Description ?? string.Empty,
                        Reference = entry.Reference ?? string.Empty,
                        Status = entry.Status.ToString(),
                        BranchName = entry.BranchName,
                        CreatedByName = createdByName,
                        TotalAmount = entry.TotalDebit,
                        TotalDebit = entry.TotalDebit,
                        TotalCredit = entry.TotalCredit,
                        LinesCount = entry.LinesCount,
                        StatusDisplay = info.Text,
                        StatusClass = info.CssClass,
                        DateFormatted = entry.Date.ToString("dd/MM/yyyy"),
                        DateGroup = entry.Date.ToString("yyyy-MM"),
                        TotalAmountFormatted = entry.TotalDebit.ToString("N2"),
                        TotalDebitFormatted = entry.TotalDebit.ToString("N2"),
                        TotalCreditFormatted = entry.TotalCredit.ToString("N2"),
                        IsDraft = entry.Status == JournalEntryStatus.Draft,
                        CanDelete = entry.Status == JournalEntryStatus.Draft || entry.Status == JournalEntryStatus.Posted,
                        IsBalanced = isBalanced
                    };
                });

            var operation = new DataOperations();

            if (dm.Search != null && dm.Search.Count > 0)
            {
                dataSource = operation.PerformSearching(dataSource, dm.Search);
            }

            if (dm.Where != null && dm.Where.Count > 0)
            {
                dataSource = operation.PerformFiltering(dataSource, dm.Where, dm.Where[0].Operator);
            }

            var count = dataSource.Count();

            if (dm.Sorted != null && dm.Sorted.Count > 0)
            {
                dataSource = operation.PerformSorting(dataSource, dm.Sorted);
            }

            if (dm.Skip != 0)
            {
                dataSource = operation.PerformSkip(dataSource, dm.Skip);
            }

            if (dm.Take != 0)
            {
                dataSource = operation.PerformTake(dataSource, dm.Take);
            }

            return dm.RequiresCounts ? Json(new { result = dataSource, count }) : Json(dataSource);
        }

        [HttpGet]
        [Authorize(Policy = "journal.view")]
        public async Task<IActionResult> Summary()
        {
            var filters = ReadFilterParameters(Request);

            var query = _context.JournalEntries
                .AsNoTracking()
                .Include(j => j.Branch)
                .Include(j => j.CreatedBy)
                .Include(j => j.Lines)
                .AsQueryable();

            query = ApplyFilters(query, filters);

            var totalEntries = await query.CountAsync();

            var statusCounts = await query
                .GroupBy(entry => entry.Status)
                .Select(group => new { Status = group.Key, Count = group.Count() })
                .ToListAsync();

            var unbalancedEntries = await query
                .Where(entry => entry.Lines.Sum(line => line.DebitAmount) != entry.Lines.Sum(line => line.CreditAmount))
                .CountAsync();

            var totalDebit = await query
                .SelectMany(entry => entry.Lines.Select(line => (decimal?)line.DebitAmount))
                .SumAsync() ?? 0m;

            var totalCredit = await query
                .SelectMany(entry => entry.Lines.Select(line => (decimal?)line.CreditAmount))
                .SumAsync() ?? 0m;

            var statusSummary = statusCounts.Count == 0
                ? string.Empty
                : string.Join(" • ", statusCounts.Select(sc =>
                {
                    var info = GetStatusInfo(sc.Status);
                    return string.Concat(info.Text, ": ", sc.Count);
                }));

            return Json(new
            {
                totalEntries,
                unbalancedEntries,
                totalDebit,
                totalCredit,
                statusSummary
            });
        }

        // GET: JournalEntries/Create
        [Authorize(Policy = "journal.create")]
        public async Task<IActionResult> Create()
        {
            var viewModel = new CreateJournalEntryViewModel
            {
                Date = DateTime.Now,
                Number = await GenerateJournalEntryNumber()
            };

            await PopulateDropdowns(viewModel);
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "journal.create")]
        public async Task<IActionResult> Create(CreateJournalEntryViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdowns(model);
                return View(model);
            }

            if (model.Lines == null || model.Lines.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "يجب إضافة بند واحد على الأقل");
                await PopulateDropdowns(model);
                return View(model);
            }

            var totalDebit = Math.Round(model.Lines.Sum(l => l.DebitAmount), 2, MidpointRounding.AwayFromZero);
            var totalCredit = Math.Round(model.Lines.Sum(l => l.CreditAmount), 2, MidpointRounding.AwayFromZero);

            if (totalDebit != totalCredit)
            {
                ModelState.AddModelError(string.Empty, "القيد غير متوازن");
                await PopulateDropdowns(model);
                return View(model);
            }

            var accountIds = model.Lines.Select(l => l.AccountId).Distinct().ToList();
            var currencies = await _context.Accounts
                .Where(a => accountIds.Contains(a.Id))
                .Select(a => a.CurrencyId)
                .Distinct()
                .ToListAsync();

            if (currencies.Count > 1)
            {
                ModelState.AddModelError(string.Empty, "يجب أن تكون جميع الحسابات بنفس العملة");
                await PopulateDropdowns(model);
                return View(model);
            }

            var createdById = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var lines = model.Lines.Select(line => new JournalEntryLine
            {
                AccountId = line.AccountId,
                Description = line.Description,
                DebitAmount = line.DebitAmount,
                CreditAmount = line.CreditAmount,
                CostCenterId = line.CostCenterId
            }).ToList();

            try
            {
                await _journalEntryService.CreateJournalEntryAsync(
                    model.Date,
                    model.Description,
                    model.BranchId,
                    createdById,
                    lines,
                    JournalEntryStatus.Draft,
                    model.Reference,
                    model.Number);

                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }

            await PopulateDropdowns(model);
            return View(model);
        }

        private async Task<string> GenerateJournalEntryNumber()
        {
            var year = DateTime.Now.Year;
            var lastEntry = await _context.JournalEntries
                .Where(j => j.Date.Year == year)
                .OrderByDescending(j => j.Number)
                .FirstOrDefaultAsync();

            if (lastEntry == null)
                return $"JE{year}001";

            var lastNumber = lastEntry.Number.Substring(6);
            if (int.TryParse(lastNumber, out int number))
                return $"JE{year}{(number + 1):D3}";

            return $"JE{year}001";
        }

        private async Task PopulateDropdowns(CreateJournalEntryViewModel model)
        {
            model.Branches = await _context.Branches
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.NameAr
                }).ToListAsync();

            model.CostCenters = await _context.CostCenters
                .OrderBy(c => c.NameAr)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.NameAr
                }).ToListAsync();

            model.CostCenters.Insert(0, new SelectListItem
            {
                Value = string.Empty,
                Text = "بدون"
            });

            model.Accounts = await _context.Accounts
                .Where(a => a.CanPostTransactions)
                .Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = $"{a.Code} - {a.NameAr} ({a.Currency.Code})"
                }).ToListAsync();
        }

        [Authorize(Policy = "journal.view")]
        public async Task<IActionResult> Details(int id)
        {
            var entry = await _context.JournalEntries
                .Include(j => j.Branch)
                .Include(j => j.Lines)
                    .ThenInclude(l => l.Account).ThenInclude(c => c.Currency)
                .Include(j => j.Lines)
                    .ThenInclude(l => l.CostCenter)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (entry == null)
                return NotFound();

            var model = MapToDetailsViewModel(entry);

            return View(model);
        }

        [Authorize(Policy = "journal.view")]
        public async Task<IActionResult> Print(int id)
        {
            var entry = await _context.JournalEntries
                .Include(j => j.Branch)
                .Include(j => j.Lines)
                    .ThenInclude(l => l.Account)
                        .ThenInclude(a => a.Currency)
                .Include(j => j.Lines)
                    .ThenInclude(l => l.CostCenter)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (entry == null)
                return NotFound();

            var model = MapToDetailsViewModel(entry);

            return View(model);
        }

        // GET: JournalEntries/Edit/5
        [Authorize(Policy = "journal.edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var entry = await _context.JournalEntries
                .Include(j => j.Lines)
                    .ThenInclude(l => l.Account)
                .Include(j => j.Lines)
                    .ThenInclude(l => l.CostCenter)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (entry == null)
                return NotFound();

            var model = new CreateJournalEntryViewModel
            {
                Id = entry.Id,
                Number = entry.Number,
                Date = entry.Date,
                Description = entry.Description,
                Reference = entry.Reference,
                BranchId = entry.BranchId,
                Lines = entry.Lines.Select(l => new JournalEntryLineViewModel
                {
                    AccountId = l.AccountId,
                    AccountCode = l.Account?.Code ?? string.Empty,
                    AccountName = l.Account?.NameAr ?? string.Empty,
                    Description = l.Description ?? string.Empty,
                    DebitAmount = l.DebitAmount,
                    CreditAmount = l.CreditAmount,
                    CostCenterId = l.CostCenterId,
                    CostCenterName = l.CostCenter?.NameAr ?? string.Empty
                }).ToList()
            };

            await PopulateDropdowns(model);
            return View(model);
        }

        // POST: JournalEntries/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "journal.edit")]
        public async Task<IActionResult> Edit(int id, CreateJournalEntryViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            if (!ModelState.IsValid || model.Lines == null || model.Lines.Count == 0 ||
                Math.Round(model.Lines.Sum(l => l.DebitAmount), 2) != Math.Round(model.Lines.Sum(l => l.CreditAmount), 2))
            {
                if (Math.Round(model.Lines?.Sum(l => l.DebitAmount) ?? 0, 2) != Math.Round(model.Lines?.Sum(l => l.CreditAmount) ?? 0, 2))
                    ModelState.AddModelError(string.Empty, "القيد غير متوازن");

                await PopulateDropdowns(model);
                return View(model);
            }

            var accountIds = model.Lines.Select(l => l.AccountId).Distinct().ToList();
            var currencies = await _context.Accounts
                .Where(a => accountIds.Contains(a.Id))
                .Select(a => a.CurrencyId)
                .Distinct()
                .ToListAsync();
            if (currencies.Count > 1)
            {
                ModelState.AddModelError(string.Empty, "يجب أن تكون جميع الحسابات بنفس العملة");
                await PopulateDropdowns(model);
                return View(model);
            }

            var entry = await _context.JournalEntries
                .Include(j => j.Lines)
                .FirstOrDefaultAsync(j => j.Id == id);
            if (entry == null)
                return NotFound();

            entry.Date = model.Date;
            entry.Description = model.Description;
            entry.Reference = model.Reference;
            entry.BranchId = model.BranchId;
            entry.TotalDebit = model.Lines.Sum(l => l.DebitAmount);
            entry.TotalCredit = model.Lines.Sum(l => l.CreditAmount);
            entry.UpdatedAt = DateTime.Now;

            _context.JournalEntryLines.RemoveRange(entry.Lines);
            entry.Lines = model.Lines.Select(l => new JournalEntryLine
            {
                AccountId = l.AccountId,
                Description = l.Description,
                DebitAmount = l.DebitAmount,
                CreditAmount = l.CreditAmount,
                CostCenterId = l.CostCenterId
            }).ToList();

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "journal.edit")]
        public async Task<IActionResult> Post(int id)
        {
            var entry = await _context.JournalEntries
                .Include(j => j.Lines)
                    .ThenInclude(l => l.Account)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (entry == null)
                return NotFound();

            if (entry.Status != JournalEntryStatus.Draft)
                return BadRequest();

            if (!entry.IsBalanced)
                return BadRequest("القيد غير متوازن");

            foreach (var line in entry.Lines)
            {
                var account = line.Account;
                var netAmount = account.Nature == AccountNature.Debit
                    ? line.DebitAmount - line.CreditAmount
                    : line.CreditAmount - line.DebitAmount;

                account.CurrentBalance += netAmount;
                account.UpdatedAt = DateTime.Now;
            }

            entry.Status = JournalEntryStatus.Posted;
            entry.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Ok(new { success = true });
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "journal.delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var entry = await _context.JournalEntries
                .Include(j => j.Lines)
                    .ThenInclude(l => l.Account)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (entry == null)
            {
                return NotFound();
            }

            if (entry.Status != JournalEntryStatus.Draft && entry.Status != JournalEntryStatus.Posted)
            {
                return BadRequest("لا يمكن إلغاء هذا القيد في حالته الحالية.");
            }

            var relatedEntities = new List<string>();

            if (await _context.SalaryPayments.AnyAsync(p => p.JournalEntryId == id))
            {
                relatedEntities.Add("سندات صرف الرواتب");
            }

            if (await _context.EmployeeAdvances.AnyAsync(a => a.JournalEntryId == id))
            {
                relatedEntities.Add("سلف الموظفين");
            }

            if (await _context.PaymentTransfers.AnyAsync(t => t.JournalEntryId == id))
            {
                relatedEntities.Add("تحويلات الحسابات");
            }

            if (await _context.Expenses.AnyAsync(e => e.JournalEntryId == id))
            {
                relatedEntities.Add("المصروفات");
            }

            if (await _context.CompoundJournalExecutionLogs.AnyAsync(l => l.JournalEntryId == id))
            {
                relatedEntities.Add("سجل تنفيذ القيود المركبة");
            }

            if (relatedEntities.Count > 0)
            {
                var relatedText = string.Join("، ", relatedEntities);
                return BadRequest($"لا يمكن إلغاء هذا القيد لأنه مرتبط بـ: {relatedText}. يرجى حذف السجلات المرتبطة أولاً.");
            }

            if (entry.Status == JournalEntryStatus.Posted)
            {
                foreach (var line in entry.Lines)
                {
                    var account = line.Account;
                    var netAmount = account.Nature == AccountNature.Debit
                        ? line.DebitAmount - line.CreditAmount
                        : line.CreditAmount - line.DebitAmount;

                    account.CurrentBalance -= netAmount;
                    account.UpdatedAt = DateTime.Now;
                }
            }

            entry.Status = JournalEntryStatus.Cancelled;
            entry.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Ok(new { success = true });
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Policy = "journal.view")]
        public async Task<IActionResult> Draft(string? searchTerm, int page = 1, int pageSize = 10)
        {
            return await GetEntriesByStatus(JournalEntryStatus.Draft, "Draft", searchTerm, page, pageSize);
        }

        [Authorize(Policy = "journal.view")]
        public async Task<IActionResult> Posted(string? searchTerm, int page = 1, int pageSize = 10)
        {
            return await GetEntriesByStatus(JournalEntryStatus.Posted, "Posted", searchTerm, page, pageSize);
        }

        private string? GetColumnSearchValue(int index)
        {
            var value = Request.Query[$"columns[{index}][search][value]"].FirstOrDefault();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static IQueryable<JournalEntry> ApplyOrdering(IQueryable<JournalEntry> query, int columnIndex, string? direction)
        {
            var ascending = string.Equals(direction, "asc", StringComparison.OrdinalIgnoreCase);

            return columnIndex switch
            {
                0 => ascending ? query.OrderBy(j => j.Number) : query.OrderByDescending(j => j.Number),
                1 => ascending ? query.OrderBy(j => j.Date) : query.OrderByDescending(j => j.Date),
                2 => ascending ? query.OrderBy(j => j.Description) : query.OrderByDescending(j => j.Description),
                3 => ascending ? query.OrderBy(j => j.Reference) : query.OrderByDescending(j => j.Reference),
                4 => ascending ? query.OrderBy(j => j.Branch.NameAr) : query.OrderByDescending(j => j.Branch.NameAr),
                5 => ascending
                    ? query.OrderBy(j =>
                        (j.CreatedBy.FirstName ?? string.Empty) + " " + (j.CreatedBy.LastName ?? string.Empty) + " " + (j.CreatedBy.UserName ?? string.Empty))
                    : query.OrderByDescending(j =>
                        (j.CreatedBy.FirstName ?? string.Empty) + " " + (j.CreatedBy.LastName ?? string.Empty) + " " + (j.CreatedBy.UserName ?? string.Empty)),
                6 => ascending ? query.OrderBy(j => j.Lines.Sum(l => l.DebitAmount)) : query.OrderByDescending(j => j.Lines.Sum(l => l.DebitAmount)),
                7 => ascending ? query.OrderBy(j => j.Lines.Count) : query.OrderByDescending(j => j.Lines.Count),
                8 => ascending ? query.OrderBy(j => j.Status) : query.OrderByDescending(j => j.Status),
                _ => ascending ? query.OrderBy(j => j.Date) : query.OrderByDescending(j => j.Date)
            };
        }

        public sealed class JournalEntryManagementRequest
        {
            public int Page { get; set; } = 1;
            public int PageSize { get; set; } = 50;
            public string? SortColumn { get; set; }
            public string? SortDirection { get; set; }
            public JournalEntryManagementFilters? Filters { get; set; }
        }

        private sealed class JournalEntryManagementFilters
        {
            public string? SearchTerm { get; set; }
            public string? Number { get; set; }
            public string? Description { get; set; }
            public string? Reference { get; set; }
            public int? BranchId { get; set; }
            public string? Status { get; set; }
            public string? CreatedBy { get; set; }
            public DateTime? FromDate { get; set; }
            public DateTime? ToDate { get; set; }
            public decimal? MinAmount { get; set; }
            public decimal? MaxAmount { get; set; }
            public bool ShowUnbalancedOnly { get; set; }
        }

        private static (string Text, string CssClass) GetStatusInfo(JournalEntryStatus status)
        {
            return status switch
            {
                JournalEntryStatus.Draft => ("مسودة", "bg-secondary"),
                JournalEntryStatus.Posted => ("مرحل", "bg-success"),
                JournalEntryStatus.Approved => ("معتمد", "bg-primary"),
                JournalEntryStatus.Cancelled => ("ملغي", "bg-danger"),
                _ => (status.ToString(), "bg-secondary")
            };
        }

        private static JournalEntryDetailsViewModel MapToDetailsViewModel(JournalEntry entry)
        {
            return new JournalEntryDetailsViewModel
            {
                Id = entry.Id,
                Number = entry.Number,
                Date = entry.Date,
                Description = entry.Description,
                Reference = entry.Reference,
                Status = entry.Status.ToString(),
                BranchName = entry.Branch.NameAr,
                Lines = entry.Lines.Select(l => new JournalEntryLineViewModel
                {
                    AccountId = l.AccountId,
                    AccountCode = l.Account.Code,
                    AccountName = $"{l.Account.NameAr} ({l.Account?.Currency?.Code})",
                    Description = l.Description ?? string.Empty,
                    CostCenterId = l.CostCenterId,
                    CostCenterName = l.CostCenter?.NameAr,
                    DebitAmount = l.DebitAmount,
                    CreditAmount = l.CreditAmount
                }).ToList(),
                TotalDebit = entry.Lines.Sum(l => l.DebitAmount),
                TotalCredit = entry.Lines.Sum(l => l.CreditAmount)
            };
        }

        private async Task<IActionResult> GetEntriesByStatus(JournalEntryStatus status, string viewName, string? searchTerm, int page, int pageSize)
        {
            var query = _context.JournalEntries
                .Include(j => j.Branch)
                .Include(j => j.Lines)
                .Where(j => j.Status == status)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(j =>
                    j.Number.Contains(searchTerm) ||
                    j.Description.Contains(searchTerm) ||
                    (j.Reference != null && j.Reference.Contains(searchTerm)) ||
                    j.Branch.NameAr.Contains(searchTerm));
            }

            var totalItems = await query.CountAsync();

            var entries = await query
                .OrderByDescending(j => j.Date)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = entries.Select(j => new JournalEntryViewModel
            {
                Id = j.Id,
                Number = j.Number,
                Date = j.Date,
                Description = j.Description,
                Reference = j.Reference,
                Status = j.Status.ToString(),
                BranchName = j.Branch.NameAr,
                TotalAmount = j.Lines.Sum(l => l.DebitAmount),
                LinesCount = j.Lines.Count,
                CanDelete = j.Status == JournalEntryStatus.Draft || j.Status == JournalEntryStatus.Posted
            }).ToList();

            var result = new PagedResult<JournalEntryViewModel>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                SearchTerm = searchTerm
            };

            return View(viewName, result);
        }
    }
}
