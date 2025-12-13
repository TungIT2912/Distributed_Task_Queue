namespace Coordinator.DTOs;

public class WorkerInfo
{
    public int Id { get; set; }
    public string WorkerId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? HostAddress { get; set; }
    public int Port { get; set; }
    public DateTime RegisteredAt { get; set; }
    public DateTime? LastHeartbeat { get; set; }
    public int TasksProcessed { get; set; }
    public int TasksFailed { get; set; }
}

