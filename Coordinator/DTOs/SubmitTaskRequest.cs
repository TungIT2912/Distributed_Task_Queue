using System.ComponentModel.DataAnnotations;

namespace Coordinator.DTOs;

public class SubmitTaskRequest
{
    [Required]
    public string TaskType { get; set; } = "Default";

    [Range(0, 10)]
    public int Priority { get; set; } = 0;

    [Required]
    public string Payload { get; set; } = "{}";
}
