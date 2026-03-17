// using System.Text.Json;
// using WorkerNode.Models;

// namespace WorkerNode.Services;

// public class TaskProcessor
// {
//     private readonly ILogger<TaskProcessor> _logger;

//     public TaskProcessor(ILogger<TaskProcessor> logger)
//     {
//         _logger = logger;
//     }

//     public async Task<TaskResult> ProcessTaskAsync(TaskMessage task)
//     {
//         _logger.LogInformation("Processing task {TaskId} of type {TaskType}", task.TaskId, task.TaskType);

//         try
//         {
//             // Simulate task processing
//             await Task.Delay(1000 + Random.Shared.Next(2000)); // 1-3 seconds

//             // Parse payload if needed
//             var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(task.Payload);

//             // Process based on task type
//             var result = task.TaskType switch
//             {
//                 "Compute" => await ProcessComputeTaskAsync(payload),
//                 "DataProcessing" => await ProcessDataTaskAsync(payload),
//                 "Email" => await ProcessEmailTaskAsync(payload),
//                 _ => await ProcessDefaultTaskAsync(payload)
//             };

//             _logger.LogInformation("Task {TaskId} completed successfully", task.TaskId);

//             return new TaskResult
//             {
//                 Success = true,
//                 Result = result,
//                 ProcessedAt = DateTime.UtcNow
//             };
//         }
//         catch (Exception ex)
//         {
//             _logger.LogError(ex, "Error processing task {TaskId}", task.TaskId);
//             return new TaskResult
//             {
//                 Success = false,
//                 ErrorMessage = ex.Message,
//                 ProcessedAt = DateTime.UtcNow
//             };
//         }
//     }

//     private async Task<string> ProcessComputeTaskAsync(Dictionary<string, object>? payload)
//     {
//         await Task.Delay(500);
//         return JsonSerializer.Serialize(new { Status = "Computed", Result = "42" });
//     }

//     private async Task<string> ProcessDataTaskAsync(Dictionary<string, object>? payload)
//     {
//         await Task.Delay(800);
//         return JsonSerializer.Serialize(new { Status = "Processed", Records = 100 });
//     }

//     private async Task<string> ProcessEmailTaskAsync(Dictionary<string, object>? payload)
//     {
//         await Task.Delay(300);
//         return JsonSerializer.Serialize(new { Status = "Sent", MessageId = Guid.NewGuid().ToString() });
//     }

//     private async Task<string> ProcessDefaultTaskAsync(Dictionary<string, object>? payload)
//     {
//         await Task.Delay(500);
//         return JsonSerializer.Serialize(new { Status = "Completed", Timestamp = DateTime.UtcNow });
//     }
// }

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
            Dictionary<string, JsonElement>? payload = null;

            if (!string.IsNullOrEmpty(task.Payload) && task.Payload != "{}")
            {
                payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(task.Payload);
            }

            var result = task.TaskType switch
            {
                "Compute"        => await ProcessComputeTaskAsync(payload),
                "DataProcessing" => await ProcessDataTaskAsync(payload),
                "Email"          => await ProcessEmailTaskAsync(payload),
                _                => await ProcessDefaultTaskAsync(payload)
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

    // ──────────────────────────────────────────────
    // Compute: { "operation": "add", "a": 5, "b": 3 }
    // ──────────────────────────────────────────────
    private async Task<string> ProcessComputeTaskAsync(Dictionary<string, JsonElement>? payload)
    {
        await Task.Delay(300);

        if (payload == null)
            throw new ArgumentException("Payload is required for Compute task");

        if (!payload.TryGetValue("operation", out var opEl))
            throw new ArgumentException("Missing field: operation");

        if (!payload.TryGetValue("a", out var aEl) || !payload.TryGetValue("b", out var bEl))
            throw new ArgumentException("Missing fields: a, b");

        var operation = opEl.GetString() ?? throw new ArgumentException("operation must be a string");
        var a = aEl.GetDouble();
        var b = bEl.GetDouble();

        var result = operation switch
        {
            "add"      => a + b,
            "subtract" => a - b,
            "multiply" => a * b,
            "divide"   => b != 0 ? a / b : throw new DivideByZeroException("Cannot divide by zero"),
            _          => throw new ArgumentException($"Unknown operation: {operation}")
        };

        return JsonSerializer.Serialize(new
        {
            Operation = operation,
            A = a,
            B = b,
            Result = result
        });
    }

    // ──────────────────────────────────────────────
    // DataProcessing: { "inputData": [1,2,3,4,5], "action": "sum" | "average" | "max" | "min" }
    // ──────────────────────────────────────────────
    private async Task<string> ProcessDataTaskAsync(Dictionary<string, JsonElement>? payload)
    {
        await Task.Delay(500);

        if (payload == null)
            throw new ArgumentException("Payload is required for DataProcessing task");

        if (!payload.TryGetValue("inputData", out var dataEl))
            throw new ArgumentException("Missing field: inputData");

        if (!payload.TryGetValue("action", out var actionEl))
            throw new ArgumentException("Missing field: action");

        var numbers = dataEl.EnumerateArray()
                            .Select(x => x.GetDouble())
                            .ToList();

        if (numbers.Count == 0)
            throw new ArgumentException("inputData must not be empty");

        var action = actionEl.GetString() ?? "sum";

        var result = action switch
        {
            "sum"     => numbers.Sum(),
            "average" => numbers.Average(),
            "max"     => numbers.Max(),
            "min"     => numbers.Min(),
            _         => throw new ArgumentException($"Unknown action: {action}")
        };

        return JsonSerializer.Serialize(new
        {
            Action = action,
            InputCount = numbers.Count,
            Result = result
        });
    }

    // ──────────────────────────────────────────────
    // Email: { "to": "user@example.com", "subject": "Hello", "body": "..." }
    // (Hiện tại log ra console, bạn có thể tích hợp SMTP/MailKit sau)
    // ──────────────────────────────────────────────
    private async Task<string> ProcessEmailTaskAsync(Dictionary<string, JsonElement>? payload)
    {
        await Task.Delay(300);

        if (payload == null)
            throw new ArgumentException("Payload is required for Email task");

        if (!payload.TryGetValue("to", out var toEl))
            throw new ArgumentException("Missing field: to");

        if (!payload.TryGetValue("subject", out var subjectEl))
            throw new ArgumentException("Missing field: subject");

        if (!payload.TryGetValue("body", out var bodyEl))
            throw new ArgumentException("Missing field: body");

        var to      = toEl.GetString()      ?? throw new ArgumentException("to must be a string");
        var subject = subjectEl.GetString() ?? "";
        var body    = bodyEl.GetString()    ?? "";

        // TODO: tích hợp SMTP thật ở đây
        // await _smtpService.SendAsync(to, subject, body);
        _logger.LogInformation("📧 [SIMULATED] Sending email to {To} | Subject: {Subject}", to, subject);

        return JsonSerializer.Serialize(new
        {
            To = to,
            Subject = subject,
            MessageId = Guid.NewGuid().ToString(),
            SentAt = DateTime.UtcNow
        });
    }

    // ──────────────────────────────────────────────
    // Default: { "data": "anything" }
    // ──────────────────────────────────────────────
    private async Task<string> ProcessDefaultTaskAsync(Dictionary<string, JsonElement>? payload)
    {
        await Task.Delay(200);

        return JsonSerializer.Serialize(new
        {
            Status = "Completed",
            ReceivedPayload = payload,
            ProcessedAt = DateTime.UtcNow
        });
    }
}