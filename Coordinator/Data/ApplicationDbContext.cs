using Microsoft.EntityFrameworkCore;
using Coordinator.Models;

namespace Coordinator.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Worker> Workers { get; set; }
    public DbSet<Task> Tasks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // Configure Worker
        modelBuilder.Entity<Worker>(entity =>
        {
            entity.HasIndex(e => e.WorkerId).IsUnique();
        });

        // Configure Task
        modelBuilder.Entity<Task>(entity =>
        {
            entity.HasIndex(e => e.TaskId).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
        });
    }
}

