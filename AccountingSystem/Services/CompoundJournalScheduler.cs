using AccountingSystem.Data;
using AccountingSystem.Models.CompoundJournals;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AccountingSystem.Services
{
    public class CompoundJournalScheduler : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CompoundJournalScheduler> _logger;
        private readonly TimeSpan _pollingInterval;

        public CompoundJournalScheduler(IServiceProvider serviceProvider, ILogger<CompoundJournalScheduler> logger, IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            var seconds = configuration.GetValue<int?>("CompoundJournals:PollingIntervalSeconds") ?? 60;
            if (seconds < 10)
            {
                seconds = 10;
            }

            _pollingInterval = TimeSpan.FromSeconds(seconds);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Compound journal scheduler started with interval {Interval}", _pollingInterval);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessDueDefinitionsAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error while processing compound journal definitions");
                }

                try
                {
                    await Task.Delay(_pollingInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task ProcessDueDefinitionsAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var service = scope.ServiceProvider.GetRequiredService<ICompoundJournalService>();
            var nowUtc = DateTime.Now;

            var dueDefinitions = await context.CompoundJournalDefinitions
                .Where(d => d.IsActive && d.TriggerType != CompoundJournalTriggerType.Manual && d.NextRunUtc != null && d.NextRunUtc <= nowUtc)
                .ToListAsync(cancellationToken);

            foreach (var definition in dueDefinitions)
            {
                var userId = string.IsNullOrWhiteSpace(definition.CreatedById) ? "system" : definition.CreatedById;
                try
                {
                    _logger.LogInformation("Executing compound journal definition {DefinitionId} - {Name}", definition.Id, definition.Name);
                    await service.ExecuteAsync(definition.Id, new CompoundJournalExecutionRequest
                    {
                        ExecutionDate = nowUtc,
                        IsAutomatic = true,
                        UserId = userId
                    }, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to execute compound journal definition {DefinitionId}", definition.Id);
                    context.CompoundJournalExecutionLogs.Add(new CompoundJournalExecutionLog
                    {
                        DefinitionId = definition.Id,
                        ExecutedAtUtc = nowUtc,
                        IsAutomatic = true,
                        Status = CompoundJournalExecutionStatus.Failed,
                        Message = ex.Message,
                        ContextSnapshotJson = "{}"
                    });

                    definition.LastRunUtc = nowUtc;
                    var next = service.CalculateNextRun(definition, nowUtc);
                    if (next.HasValue)
                    {
                        definition.NextRunUtc = next;
                    }

                    await context.SaveChangesAsync(cancellationToken);
                }
            }
        }
    }
}
