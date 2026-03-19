using Coordinator.Data;
using Microsoft.EntityFrameworkCore;

namespace Coordinator.Services;

public class StaleTaskMonitor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StaleTaskMonitor> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);
    private TimeSpan _taskTimeout = TimeSpan.FromSeconds(60);

    public StaleTaskMonitor(IServiceProvider serviceProvider, ILogger<StaleTaskMonitor> logger, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        var timeoutSeconds = int.Parse(configuration.GetSection("TaskQueue")["TaskTimeoutSeconds"] ?? "60");
        _taskTimeout = TimeSpan.FromSeconds(timeoutSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("StaleTaskMonitor started. Task timeout: {Timeout}s", _taskTimeout.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ReassignStaleTasksAsync(); }
            catch (Exception ex) { _logger.LogError(ex, "Error in StaleTaskMonitor"); }
            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task ReassignStaleTasksAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var cutoff = DateTime.UtcNow - _taskTimeout;

        var staleTasks = await db.Tasks
            .Where(t => t.Status == "Processing" && t.StartedAt != null && t.StartedAt < cutoff)
            .ToListAsync();

        if (!staleTasks.Any()) return;

        foreach (var task in staleTasks)
        {
            if (task.WorkerId != null)
            {
                var worker = await db.Workers.FirstOrDefaultAsync(w => w.Id == task.WorkerId);
                if (worker != null)
                {
                    worker.Status = "Inactive";
                    _logger.LogWarning("Worker {WorkerId} marked Inactive — missed heartbeats", worker.WorkerId);
                }
            }

            task.Status = "Pending";
            task.WorkerId = null;
            task.StartedAt = null;
            task.RetryCount += 1;

            _logger.LogWarning("Task {TaskId} reassigned (retry #{Retry}) — worker went silent", task.TaskId, task.RetryCount);
        }

        await db.SaveChangesAsync();
        _logger.LogInformation("Reassigned {Count} stale tasks", staleTasks.Count);
    }
}