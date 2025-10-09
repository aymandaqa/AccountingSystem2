using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Models.CompoundJournals;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Services
{
    public class CompoundJournalService : ICompoundJournalService
    {
        private static readonly Regex PlaceholderRegex = new("\\{(?<key>[^}]+)\\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private readonly ApplicationDbContext _context;
        private readonly IJournalEntryService _journalEntryService;
        private readonly JsonSerializerOptions _jsonOptions;

        public CompoundJournalService(ApplicationDbContext context, IJournalEntryService journalEntryService)
        {
            _context = context;
            _journalEntryService = journalEntryService;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() }
            };
        }

        public async Task<CompoundJournalTemplate> ParseTemplateAsync(string templateJson, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(templateJson))
            {
                throw new ArgumentException("Template JSON cannot be empty", nameof(templateJson));
            }

            try
            {
                var template = JsonSerializer.Deserialize<CompoundJournalTemplate>(templateJson, _jsonOptions);
                if (template == null)
                {
                    throw new InvalidOperationException("Template JSON is invalid");
                }

                if (template.Lines == null || template.Lines.Count == 0)
                {
                    throw new InvalidOperationException("Template must contain at least one line definition");
                }

                foreach (var line in template.Lines)
                {
                    if (line.AccountId <= 0)
                    {
                        throw new InvalidOperationException("Each line must specify a valid accountId");
                    }
                }

                return template;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"تعذر قراءة قالب القيد المركب: {ex.Message}", ex);
            }
        }

        public async Task<CompoundJournalExecutionResult> ExecuteAsync(int definitionId, CompoundJournalExecutionRequest request, CancellationToken cancellationToken = default)
        {
            var definition = await _context.CompoundJournalDefinitions
                .Include(d => d.ExecutionLogs)
                .FirstOrDefaultAsync(d => d.Id == definitionId, cancellationToken)
                ?? throw new KeyNotFoundException($"لم يتم العثور على تعريف قيد مركب بالرقم {definitionId}");

            var template = await ParseTemplateAsync(definition.TemplateJson, cancellationToken);

            var context = BuildExecutionContext(template, request.ContextOverrides);
            var conditionResult = EvaluateConditions(template, context);
            if (!conditionResult.Success)
            {
                UpdateDefinitionAfterRun(definition, request);
                await LogExecution(definition, request, null, CompoundJournalExecutionStatus.Skipped, conditionResult.Message, context, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                return new CompoundJournalExecutionResult
                {
                    Success = false,
                    Status = CompoundJournalExecutionStatus.Skipped,
                    Message = conditionResult.Message,
                    ContextSnapshot = context
                };
            }

            var lines = new List<JournalEntryLine>();
            foreach (var lineTemplate in template.Lines)
            {
                var debitAmount = EvaluateValue(lineTemplate.Debit, context);
                var creditAmount = EvaluateValue(lineTemplate.Credit, context);

                if (debitAmount == 0m && creditAmount == 0m)
                {
                    continue;
                }

                lines.Add(new JournalEntryLine
                {
                    AccountId = lineTemplate.AccountId,
                    Description = lineTemplate.Description,
                    DebitAmount = debitAmount,
                    CreditAmount = creditAmount,
                    CostCenterId = lineTemplate.CostCenterId
                });
            }

            if (!lines.Any())
            {
                const string noLinesMessage = "لم يتم إنشاء أي بنود بعد تقييم القالب";
                UpdateDefinitionAfterRun(definition, request);
                await LogExecution(definition, request, null, CompoundJournalExecutionStatus.Skipped, noLinesMessage, context, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                return new CompoundJournalExecutionResult
                {
                    Success = false,
                    Status = CompoundJournalExecutionStatus.Skipped,
                    Message = noLinesMessage,
                    ContextSnapshot = context
                };
            }

            var branchId = request.BranchIdOverride ?? template.BranchId;
            if (branchId == null)
            {
                throw new InvalidOperationException("يجب تحديد الفرع إما في القالب أو أثناء التنفيذ");
            }

            var description = request.DescriptionOverride ?? template.Description ?? definition.Name;
            var journalStatus = request.StatusOverride ?? template.Status;
            var journalDate = request.JournalDate?.Date ?? request.ExecutionDate.ToLocalTime().Date;

            var journalEntry = await _journalEntryService.CreateJournalEntryAsync(
                journalDate,
                description,
                branchId.Value,
                request.UserId,
                lines,
                journalStatus,
                request.ReferenceOverride);

            await LogExecution(definition, request, journalEntry, CompoundJournalExecutionStatus.Success, null, context, cancellationToken);

            UpdateDefinitionAfterRun(definition, request);

            await _context.SaveChangesAsync(cancellationToken);

            return new CompoundJournalExecutionResult
            {
                Success = true,
                Status = CompoundJournalExecutionStatus.Success,
                JournalEntryId = journalEntry.Id,
                ContextSnapshot = context
            };
        }

        public DateTime? CalculateNextRun(CompoundJournalDefinition definition, DateTime fromUtc)
        {
            if (!definition.IsActive)
            {
                return null;
            }

            switch (definition.TriggerType)
            {
                case CompoundJournalTriggerType.Manual:
                    return null;
                case CompoundJournalTriggerType.OneTime:
                    if (definition.NextRunUtc.HasValue && definition.NextRunUtc > fromUtc)
                    {
                        return definition.NextRunUtc;
                    }
                    return null;
                case CompoundJournalTriggerType.Recurring:
                    {
                        if (!definition.NextRunUtc.HasValue)
                        {
                            var initial = definition.StartDateUtc ?? fromUtc;
                            if (initial <= fromUtc)
                            {
                                initial = AddInterval(initial, definition);
                            }
                            return ApplyEndDate(definition, initial);
                        }

                        var candidate = definition.NextRunUtc.Value;
                        if (candidate <= fromUtc)
                        {
                            do
                            {
                                candidate = AddInterval(candidate, definition);
                            } while (candidate <= fromUtc);
                        }

                        return ApplyEndDate(definition, candidate);
                    }
                default:
                    return null;
            }
        }

        private static DateTime? ApplyEndDate(CompoundJournalDefinition definition, DateTime candidate)
        {
            if (definition.EndDateUtc.HasValue && candidate > definition.EndDateUtc.Value)
            {
                return null;
            }

            return candidate;
        }

        private static DateTime AddInterval(DateTime baseDate, CompoundJournalDefinition definition)
        {
            var interval = Math.Max(definition.RecurrenceInterval ?? 1, 1);
            return definition.Recurrence switch
            {
                CompoundJournalRecurrence.Daily => baseDate.AddDays(interval),
                CompoundJournalRecurrence.Weekly => baseDate.AddDays(7 * interval),
                CompoundJournalRecurrence.Monthly => baseDate.AddMonths(interval),
                CompoundJournalRecurrence.Yearly => baseDate.AddYears(interval),
                _ => baseDate.AddDays(interval)
            };
        }

        private async Task LogExecution(CompoundJournalDefinition definition, CompoundJournalExecutionRequest request, JournalEntry? journalEntry, CompoundJournalExecutionStatus status, string? message, Dictionary<string, string> context, CancellationToken cancellationToken)
        {
            var log = new CompoundJournalExecutionLog
            {
                DefinitionId = definition.Id,
                ExecutedAtUtc = request.ExecutionDate,
                IsAutomatic = request.IsAutomatic,
                JournalEntryId = journalEntry?.Id,
                Message = message,
                Status = status,
                ContextSnapshotJson = JsonSerializer.Serialize(context, _jsonOptions)
            };

            _context.CompoundJournalExecutionLogs.Add(log);
            await _context.SaveChangesAsync(cancellationToken);
        }

        private void UpdateDefinitionAfterRun(CompoundJournalDefinition definition, CompoundJournalExecutionRequest request)
        {
            definition.LastRunUtc = request.ExecutionDate;
            if (definition.TriggerType != CompoundJournalTriggerType.Manual)
            {
                definition.NextRunUtc = CalculateNextRun(definition, request.ExecutionDate);
            }
        }

        private static Dictionary<string, string> BuildExecutionContext(CompoundJournalTemplate template, IDictionary<string, string>? overrides)
        {
            var context = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (template.DefaultContext != null)
            {
                foreach (var kvp in template.DefaultContext)
                {
                    context[kvp.Key] = kvp.Value;
                }
            }

            if (overrides != null)
            {
                foreach (var kvp in overrides)
                {
                    context[kvp.Key] = kvp.Value;
                }
            }

            return context;
        }

        private static (bool Success, string? Message) EvaluateConditions(CompoundJournalTemplate template, Dictionary<string, string> context)
        {
            if (template.Conditions == null || template.Conditions.Count == 0)
            {
                return (true, null);
            }

            foreach (var condition in template.Conditions)
            {
                context.TryGetValue(condition.ContextKey, out var value);

                if (!EvaluateCondition(value, condition))
                {
                    var message = $"لم تتحقق الشرط {condition.ContextKey} ({condition.Operator})";
                    return (false, message);
                }
            }

            return (true, null);
        }

        private static bool EvaluateCondition(string? contextValue, CompoundJournalCondition condition)
        {
            switch (condition.Operator)
            {
                case CompoundJournalConditionOperator.Equals:
                    return string.Equals(contextValue, condition.Value, StringComparison.OrdinalIgnoreCase);
                case CompoundJournalConditionOperator.NotEquals:
                    return !string.Equals(contextValue, condition.Value, StringComparison.OrdinalIgnoreCase);
                case CompoundJournalConditionOperator.Contains:
                    return contextValue != null && contextValue.Contains(condition.Value, StringComparison.OrdinalIgnoreCase);
                case CompoundJournalConditionOperator.NotContains:
                    return contextValue == null || !contextValue.Contains(condition.Value, StringComparison.OrdinalIgnoreCase);
                case CompoundJournalConditionOperator.Exists:
                    return contextValue != null;
                case CompoundJournalConditionOperator.NotExists:
                    return contextValue == null;
                case CompoundJournalConditionOperator.GreaterThan:
                case CompoundJournalConditionOperator.GreaterThanOrEqual:
                case CompoundJournalConditionOperator.LessThan:
                case CompoundJournalConditionOperator.LessThanOrEqual:
                    if (decimal.TryParse(contextValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var numericContext) && decimal.TryParse(condition.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var numericCondition))
                    {
                        return condition.Operator switch
                        {
                            CompoundJournalConditionOperator.GreaterThan => numericContext > numericCondition,
                            CompoundJournalConditionOperator.GreaterThanOrEqual => numericContext >= numericCondition,
                            CompoundJournalConditionOperator.LessThan => numericContext < numericCondition,
                            CompoundJournalConditionOperator.LessThanOrEqual => numericContext <= numericCondition,
                            _ => true
                        };
                    }
                    return false;
                default:
                    return false;
            }
        }

        private static decimal EvaluateValue(TemplateValue templateValue, Dictionary<string, string> context)
        {
            switch (templateValue.Type)
            {
                case TemplateValueType.Fixed:
                    return templateValue.FixedValue ?? 0m;
                case TemplateValueType.ContextValue:
                    if (templateValue.ContextKey != null && context.TryGetValue(templateValue.ContextKey, out var contextValue) && decimal.TryParse(contextValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedValue))
                    {
                        return parsedValue;
                    }
                    return 0m;
                case TemplateValueType.Expression:
                    if (!string.IsNullOrWhiteSpace(templateValue.Expression))
                    {
                        var expression = PlaceholderRegex.Replace(templateValue.Expression, m =>
                        {
                            var key = m.Groups["key"].Value;
                            if (context.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                            {
                                return value;
                            }
                            return "0";
                        });

                        try
                        {
                            var dataTable = new DataTable();
                            var result = dataTable.Compute(expression, string.Empty);
                            return Convert.ToDecimal(result, CultureInfo.InvariantCulture);
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException($"تعذر تقييم المعادلة '{templateValue.Expression}': {ex.Message}", ex);
                        }
                    }
                    return 0m;
                default:
                    return 0m;
            }
        }
    }
}
