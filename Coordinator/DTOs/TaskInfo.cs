namespace Coordinator.DTOs;

public class TaskInfo
{
    public int Id { get; set; }
    public string TaskId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public string? WorkerId { get; set; }
}

