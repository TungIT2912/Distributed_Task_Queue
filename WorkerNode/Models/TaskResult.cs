namespace WorkerNode.Models;

public class TaskResult
{
    public bool Success { get; set; }
    public string? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ProcessedAt { get; set; }
}

