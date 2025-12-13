using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Coordinator.Models;

public class Task
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string TaskId { get; set; } = string.Empty; // Unique identifier for the task

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, Processing, Completed, Failed, Reassigned

    [Required]
    public string Payload { get; set; } = string.Empty; // JSON payload of the task

    [MaxLength(50)]
    public string TaskType { get; set; } = "Default";

    public int Priority { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? Result { get; set; }

    public string? ErrorMessage { get; set; }

    public int RetryCount { get; set; } = 0;

    public int MaxRetries { get; set; } = 3;

    // Foreign keys
    [ForeignKey("User")]
    public int? UserId { get; set; }

    public virtual User? User { get; set; }

    [ForeignKey("Worker")]
    public int? WorkerId { get; set; }

    public virtual Worker? Worker { get; set; }

    // Redis Stream entry ID for tracking
    [MaxLength(100)]
    public string? StreamEntryId { get; set; }
}

