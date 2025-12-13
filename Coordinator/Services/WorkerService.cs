using Microsoft.EntityFrameworkCore;
using Coordinator.Data;
using Coordinator.Models;
using Coordinator.DTOs;

namespace Coordinator.Services;

public class WorkerService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<WorkerService> _logger;

    public WorkerService(ApplicationDbContext context, ILogger<WorkerService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Worker?> RegisterWorkerAsync(WorkerRegistrationRequest request, int? userId = null)
    {
        // Check if worker already exists
        var existingWorker = await _context.Workers.FirstOrDefaultAsync(w => w.WorkerId == request.WorkerId);
        
        if (existingWorker != null)
        {
            // Update existing worker
            existingWorker.Status = "Active";
            existingWorker.HostAddress = request.HostAddress;
            existingWorker.Port = request.Port;
            existingWorker.LastHeartbeat = DateTime.UtcNow;
            existingWorker.UserId = userId;
            await _context.SaveChangesAsync();
            return existingWorker;
        }

        // Create new worker
        var worker = new Worker
        {
            WorkerId = request.WorkerId,
            Status = "Active",
            HostAddress = request.HostAddress,
            Port = request.Port,
            RegisteredAt = DateTime.UtcNow,
            LastHeartbeat = DateTime.UtcNow,
            UserId = userId
        };

        _context.Workers.Add(worker);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Worker {WorkerId} registered successfully", request.WorkerId);
        return worker;
    }

    public async Task<bool> UpdateHeartbeatAsync(string workerId)
    {
        var worker = await _context.Workers.FirstOrDefaultAsync(w => w.WorkerId == workerId);
        
        if (worker == null)
        {
            return false;
        }

        worker.LastHeartbeat = DateTime.UtcNow;
        worker.Status = "Active";
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<WorkerInfo>> GetActiveWorkersAsync()
    {
        var cutoffTime = DateTime.UtcNow.AddMinutes(-5); // Consider workers inactive if no heartbeat in 5 minutes
        
        // Mark stale workers as inactive
        var staleWorkers = await _context.Workers
            .Where(w => w.Status == "Active" && w.LastHeartbeat < cutoffTime)
            .ToListAsync();
        
        foreach (var worker in staleWorkers)
        {
            worker.Status = "Inactive";
        }
        
        if (staleWorkers.Any())
        {
            await _context.SaveChangesAsync();
        }

        var activeWorkers = await _context.Workers
            .Where(w => w.Status == "Active")
            .Select(w => new WorkerInfo
            {
                Id = w.Id,
                WorkerId = w.WorkerId,
                Status = w.Status,
                HostAddress = w.HostAddress,
                Port = w.Port,
                RegisteredAt = w.RegisteredAt,
                LastHeartbeat = w.LastHeartbeat,
                TasksProcessed = w.TasksProcessed,
                TasksFailed = w.TasksFailed
            })
            .ToListAsync();

        return activeWorkers;
    }

    public async Task IncrementTasksProcessedAsync(string workerId)
    {
        var worker = await _context.Workers.FirstOrDefaultAsync(w => w.WorkerId == workerId);
        if (worker != null)
        {
            worker.TasksProcessed++;
            await _context.SaveChangesAsync();
        }
    }

    public async Task IncrementTasksFailedAsync(string workerId)
    {
        var worker = await _context.Workers.FirstOrDefaultAsync(w => w.WorkerId == workerId);
        if (worker != null)
        {
            worker.TasksFailed++;
            await _context.SaveChangesAsync();
        }
    }
}

