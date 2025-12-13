using Coordinator.Data;

namespace Coordinator.Services;

public class StaleTaskMonitor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StaleTaskMonitor> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _taskTimeout = TimeSpan.FromMinutes(30);

    public StaleTaskMonitor(IServiceProvider serviceProvider, ILogger<StaleTaskMonitor> logger, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        var timeoutMinutes = int.Parse(configuration.GetSection("TaskQueue")["TaskTimeoutMinutes"] ?? "30");
        _taskTimeout = TimeSpan.FromMinutes(timeoutMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Stale Task Monitor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndReassignStaleTasksAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in stale task monitor");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task CheckAndReassignStaleTasksAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var taskService = scope.ServiceProvider.GetRequiredService<TaskService>();

        await taskService.ReassignStaleTasksAsync(_taskTimeout);
    }
}

