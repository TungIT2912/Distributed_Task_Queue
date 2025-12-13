using System.Text.Json;
using WorkerNode.Models;

namespace WorkerNode.Services;

public class TaskProcessor
{
    private readonly ILogger<TaskProcessor> _logger;

    public TaskProcessor(ILogger<TaskProcessor> logger)
    {
        _logger = logger;
    }

    public async Task<TaskResult> ProcessTaskAsync(TaskMessage task)
    {
        _logger.LogInformation("Processing task {TaskId} of type {TaskType}", task.TaskId, task.TaskType);

        try
        {
            // Simulate task processing
            await Task.Delay(1000 + Random.Shared.Next(2000)); // 1-3 seconds

            // Parse payload if needed
            var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(task.Payload);

            // Process based on task type
            var result = task.TaskType switch
            {
                "Compute" => await ProcessComputeTaskAsync(payload),
                "DataProcessing" => await ProcessDataTaskAsync(payload),
                "Email" => await ProcessEmailTaskAsync(payload),
                _ => await ProcessDefaultTaskAsync(payload)
            };

            _logger.LogInformation("Task {TaskId} completed successfully", task.TaskId);

            return new TaskResult
            {
                Success = true,
                Result = result,
                ProcessedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing task {TaskId}", task.TaskId);
            return new TaskResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ProcessedAt = DateTime.UtcNow
            };
        }
    }

    private async Task<string> ProcessComputeTaskAsync(Dictionary<string, object>? payload)
    {
        await Task.Delay(500);
        return JsonSerializer.Serialize(new { Status = "Computed", Result = "42" });
    }

    private async Task<string> ProcessDataTaskAsync(Dictionary<string, object>? payload)
    {
        await Task.Delay(800);
        return JsonSerializer.Serialize(new { Status = "Processed", Records = 100 });
    }

    private async Task<string> ProcessEmailTaskAsync(Dictionary<string, object>? payload)
    {
        await Task.Delay(300);
        return JsonSerializer.Serialize(new { Status = "Sent", MessageId = Guid.NewGuid().ToString() });
    }

    private async Task<string> ProcessDefaultTaskAsync(Dictionary<string, object>? payload)
    {
        await Task.Delay(500);
        return JsonSerializer.Serialize(new { Status = "Completed", Timestamp = DateTime.UtcNow });
    }
}

