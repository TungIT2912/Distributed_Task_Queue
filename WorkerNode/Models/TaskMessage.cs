namespace WorkerNode.Models;

public class TaskMessage
{
    public string TaskId { get; set; } = string.Empty;
    public string TaskType { get; set; } = "Default";
    public string Payload { get; set; } = string.Empty;
    public int Priority { get; set; } = 0;
    public DateTime CreatedAt { get; set; }
}

