using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Coordinator.Models;

public class Worker
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string WorkerId { get; set; } = string.Empty; // Unique identifier for the worker node

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Inactive"; // Active, Inactive, Failed

    [MaxLength(255)]
    public string? HostAddress { get; set; }

    public int Port { get; set; }

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastHeartbeat { get; set; }

    public int TasksProcessed { get; set; } = 0;

    public int TasksFailed { get; set; } = 0;

    // Foreign key to User
    [ForeignKey("User")]
    public int? UserId { get; set; }

    public virtual User? User { get; set; }

    // Navigation properties
    public virtual ICollection<Task> Tasks { get; set; } = new List<Task>();
}

