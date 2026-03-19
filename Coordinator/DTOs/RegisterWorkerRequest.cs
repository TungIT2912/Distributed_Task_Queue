// Coordinator/DTOs/RegisterWorkerRequest.cs
namespace Coordinator.DTOs;

public class RegisterWorkerRequest
{
    public string WorkerId { get; set; } = string.Empty;
    public string? HostAddress { get; set; }
    public int Port { get; set; }
}