using Microsoft.EntityFrameworkCore;
using Coordinator.Data;
using Coordinator.Models;
using Coordinator.DTOs;

namespace Coordinator.Services;

public class TaskService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TaskService> _logger;

    public TaskService(ApplicationDbContext context, ILogger<TaskService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Models.Task> CreateTaskAsync(string taskId, string payload, string taskType, int priority, int? userId, string? streamEntryId = null)
    {
        var task = new Models.Task
        {
            TaskId = taskId,
            Status = "Pending",
            Payload = payload,
            TaskType = taskType,
            Priority = priority,
            CreatedAt = DateTime.UtcNow,
            UserId = userId,
            StreamEntryId = streamEntryId
        };

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();
        return task;
    }

    public async Task<Models.Task?> UpdateTaskStatusAsync(string taskId, string status, string? result = null, string? errorMessage = null, int? workerId = null)
    {
        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.TaskId == taskId);
        
        if (task == null)
        {
            return null;
        }

        task.Status = status;
        
        if (status == "Processing" && task.StartedAt == null)
        {
            task.StartedAt = DateTime.UtcNow;
        }
        
        if (status == "Completed")
        {
            task.CompletedAt = DateTime.UtcNow;
            task.Result = result;
        }
        
        if (status == "Failed")
        {
            task.ErrorMessage = errorMessage;
            task.RetryCount++;
        }

        if (workerId.HasValue)
        {
            task.WorkerId = workerId.Value;
        }

        await _context.SaveChangesAsync();
        return task;
    }

    public async Task<List<TaskInfo>> GetTasksAsync(int? userId = null, string? status = null, int limit = 100)
    {
        var query = _context.Tasks.AsQueryable();

        if (userId.HasValue)
        {
            query = query.Where(t => t.UserId == userId);
        }

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(t => t.Status == status);
        }

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .Take(limit)
            .Select(t => new TaskInfo
            {
                Id = t.Id,
                TaskId = t.TaskId,
                Status = t.Status,
                TaskType = t.TaskType,
                Priority = t.Priority,
                CreatedAt = t.CreatedAt,
                StartedAt = t.StartedAt,
                CompletedAt = t.CompletedAt,
                Result = t.Result,
                ErrorMessage = t.ErrorMessage,
                RetryCount = t.RetryCount,
                WorkerId = t.Worker?.WorkerId
            })
            .ToListAsync();
    }

    public async Task<List<Models.Task>> GetStaleTasksAsync(TimeSpan timeout)
    {
        var cutoffTime = DateTime.UtcNow.Subtract(timeout);
        
        return await _context.Tasks
            .Where(t => t.Status == "Processing" && t.StartedAt < cutoffTime)
            .ToListAsync();
    }

    public async Task ReassignStaleTasksAsync(TimeSpan timeout)
    {
        var staleTasks = await GetStaleTasksAsync(timeout);
        
        foreach (var task in staleTasks)
        {
            task.Status = "Reassigned";
            task.RetryCount++;
            task.WorkerId = null;
            task.StartedAt = null;
            
            if (task.RetryCount >= task.MaxRetries)
            {
                task.Status = "Failed";
                task.ErrorMessage = "Task failed after maximum retries";
            }
        }

        if (staleTasks.Any())
        {
            await _context.SaveChangesAsync();
            _logger.LogWarning("Reassigned {Count} stale tasks", staleTasks.Count);
        }
    }
}

