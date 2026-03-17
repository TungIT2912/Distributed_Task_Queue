namespace Coordinator.DTOs;

public class UpdateTaskStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public string? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public int? WorkerId { get; set; }
    public string? WorkerKey { get; set; }
}

