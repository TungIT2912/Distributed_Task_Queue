namespace Coordinator.DTOs;

public class SubmitTaskResponse
{
    public string TaskId { get; set; } = string.Empty;
    public string StreamEntryId { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
}


