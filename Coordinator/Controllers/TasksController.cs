using System.Text.Json;
using Coordinator.Data;
using Coordinator.DTOs;
using Coordinator.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace Coordinator.Controllers;

[ApiController]
[Route("api/tasks")]
public class TasksController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public TasksController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [Authorize]
    [HttpPost("submit")]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitTaskRequest request,
        [FromServices] TaskService taskService,
        [FromServices] IConnectionMultiplexer redis)
    {
        var userIdClaim = User.FindFirst("UserId")?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var streamName = _configuration.GetSection("TaskQueue")["StreamName"] ?? "task-queue";

        var taskId = Guid.NewGuid().ToString();
        var createdAt = DateTime.UtcNow;

        var taskMessage = new
        {
            TaskId = taskId,
            TaskType = request.TaskType ?? "Default",
            Payload = request.Payload ?? "{}",
            Priority = request.Priority,
            CreatedAt = createdAt
        };

        var db = redis.GetDatabase();
        var entryId = await db.StreamAddAsync(streamName, new NameValueEntry[]
        {
            new("task", JsonSerializer.Serialize(taskMessage)),
            new("taskId", taskId),
            new("taskType", taskMessage.TaskType),
            new("priority", request.Priority.ToString()),
            new("createdAt", createdAt.ToString("O"))
        });

        await taskService.CreateTaskAsync(
            taskId: taskId,
            payload: taskMessage.Payload,
            taskType: taskMessage.TaskType,
            priority: request.Priority,
            userId: userId,
            streamEntryId: entryId.ToString());

        return Ok(new SubmitTaskResponse
        {
            TaskId = taskId,
            StreamEntryId = entryId.ToString(),
            Status = "Pending",
            CreatedAt = createdAt
        });
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetTasks(
        [FromServices] TaskService taskService,
        [FromQuery] string? status = null,
        [FromQuery] int limit = 100)
    {
        int? userId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out var id))
            {
                userId = id;
            }
        }

        // Admin can see all tasks (no user filter)
        if (User.IsInRole("Admin"))
        {
            userId = null;
        }

        var tasks = await taskService.GetTasksAsync(userId, status, limit);
        return Ok(tasks);
    }

    [Authorize]
    [HttpGet("{taskId}")]
    public async Task<IActionResult> GetTaskById(
        [FromRoute] string taskId,
        [FromServices] TaskService taskService)
    {
        int? userId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out var id))
            {
                userId = id;
            }
        }

        var tasks = await taskService.GetTasksAsync(userId);
        var task = tasks.FirstOrDefault(t => t.TaskId == taskId);

        if (task == null)
        {
            return NotFound();
        }

        return Ok(task);
    }

    [HttpPut("{taskId}/status")]
    public async Task<IActionResult> UpdateStatus(
        [FromRoute] string taskId,
        [FromBody] UpdateTaskStatusRequest request,
        [FromServices] TaskService taskService,
        [FromServices] ApplicationDbContext _)
    {
        var task = await taskService.UpdateTaskStatusAsync(
            taskId,
            request.Status,
            request.Result,
            request.ErrorMessage,
            request.WorkerId,
            request.WorkerKey);

        if (task == null)
        {
            return NotFound(new { message = "Task not found" });
        }

        return Ok(new TaskInfo
        {
            Id = task.Id,
            TaskId = task.TaskId,
            Status = task.Status,
            TaskType = task.TaskType,
            Priority = task.Priority,
            CreatedAt = task.CreatedAt,
            StartedAt = task.StartedAt,
            CompletedAt = task.CompletedAt,
            Result = task.Result,
            ErrorMessage = task.ErrorMessage,
            RetryCount = task.RetryCount,
            WorkerId = task.Worker?.WorkerId
        });
    }
}

