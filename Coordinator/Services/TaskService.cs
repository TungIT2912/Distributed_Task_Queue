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

    public async Task<Models.Task?> UpdateTaskStatusAsync(
        string taskId,
        string status,
        string? result = null,
        string? errorMessage = null,
        int? workerId = null,
        string? workerKey = null)
    {
        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.TaskId == taskId);
        if (task == null) return null;

        if (!workerId.HasValue && !string.IsNullOrWhiteSpace(workerKey))
        {
            var worker = await _context.Workers.FirstOrDefaultAsync(w => w.WorkerId == workerKey);
            if (worker != null)
                workerId = worker.Id;
        }

        task.Status = status;

        if (status == "Processing")
        {
            task.StartedAt = DateTime.UtcNow;
            if (workerId.HasValue)
                task.WorkerId = workerId.Value;
        }

        if (status == "Completed")
        {
            task.CompletedAt = DateTime.UtcNow;
            task.Result = result;
            if (workerId.HasValue)
                task.WorkerId = workerId.Value;
        }

        if (status == "Failed")
        {
            task.ErrorMessage = errorMessage;
            task.RetryCount++;
            if (workerId.HasValue)
                task.WorkerId = workerId.Value;
        }

        await _context.SaveChangesAsync();
        return task;
    }

    public async Task<Models.Task?> GetTaskByTaskIdAsync(string taskId)
    {
        return await _context.Tasks
            .Include(t => t.Worker)
            .FirstOrDefaultAsync(t => t.TaskId == taskId);
    }

    private static TaskInfo MapToTaskInfo(Models.Task t) => new TaskInfo
    {
        Id = t.Id,
        TaskId = t.TaskId,
        Status = t.Status,
        TaskType = t.TaskType,
        Priority = t.Priority,
        Payload = t.Payload,         
        CreatedAt = t.CreatedAt,
        StartedAt = t.StartedAt,
        CompletedAt = t.CompletedAt,
        Result = t.Result,
        ErrorMessage = t.ErrorMessage,
        RetryCount = t.RetryCount,
        WorkerId = t.Worker?.WorkerId
    };

    public async Task<List<TaskInfo>> GetTasksAsync(int? userId = null, string? status = null, int limit = 100)
    {
        var query = _context.Tasks.Include(t => t.Worker).AsQueryable();

        if (userId.HasValue)
            query = query.Where(t => t.UserId == userId);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(t => t.Status == status);

        var tasks = await query
            .OrderByDescending(t => t.CreatedAt)
            .Take(limit)
            .ToListAsync();

        return tasks.Select(MapToTaskInfo).ToList();
    }

    public async Task<List<TaskInfo>> GetTasksForWorkerAsync(int workerUserId, string? status = null, int limit = 100)
    {
        var worker = await _context.Workers.FirstOrDefaultAsync(w => w.UserId == workerUserId);

        IQueryable<Models.Task> query;

        if (worker != null)
        {
            query = _context.Tasks
                .Include(t => t.Worker)
                .Where(t => t.Status == "Pending" || t.WorkerId == worker.Id);
        }
        else
        {
            query = _context.Tasks
                .Include(t => t.Worker)
                .Where(t => t.Status == "Pending");
        }

        if (!string.IsNullOrEmpty(status))
            query = query.Where(t => t.Status == status);

        var tasks = await query
            .OrderByDescending(t => t.Priority)
            .ThenByDescending(t => t.CreatedAt)
            .Take(limit)
            .ToListAsync();

        return tasks.Select(MapToTaskInfo).ToList();
    }

    public async Task<List<Models.Task>> GetStaleTasksAsync(TimeSpan timeout)
    {
        var cutoffTime = DateTime.UtcNow.Subtract(timeout);
        return await _context.Tasks
            .Where(t => t.Status == "Processing" && t.StartedAt < cutoffTime)
            .ToListAsync();
    }

    public async System.Threading.Tasks.Task ReassignStaleTasksAsync(TimeSpan timeout)
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