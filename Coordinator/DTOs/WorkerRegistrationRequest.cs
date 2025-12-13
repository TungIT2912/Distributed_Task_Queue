namespace Coordinator.DTOs;

public class WorkerRegistrationRequest
{
    public string WorkerId { get; set; } = string.Empty;
    public string? HostAddress { get; set; }
    public int Port { get; set; }
}

