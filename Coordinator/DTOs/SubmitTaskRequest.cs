namespace Coordinator.DTOs;

public class SubmitTaskRequest
{
    public string TaskType { get; set; } = "Default";
    public int Priority { get; set; } = 0;
    public string Payload { get; set; } = "{}";
}


