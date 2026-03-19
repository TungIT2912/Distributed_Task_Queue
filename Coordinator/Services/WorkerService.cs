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
        var existing = await _context.Workers.FirstOrDefaultAsync(w => w.WorkerId == request.WorkerId);

        if (existing != null)
        {
            existing.Status = "Active";
            existing.HostAddress = request.HostAddress;
            existing.Port = request.Port;
            existing.LastHeartbeat = DateTime.UtcNow;
            if (userId.HasValue) existing.UserId = userId;
            await _context.SaveChangesAsync();
            return existing;
        }

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
        _logger.LogInformation("Worker {WorkerId} registered as Active", request.WorkerId);
        return worker;
    }

    public async System.Threading.Tasks.Task SetInactiveAsync(string workerId)
    {
        var worker = await _context.Workers.FirstOrDefaultAsync(w => w.WorkerId == workerId);
        if (worker == null) return;
        worker.Status = "Inactive";
        await _context.SaveChangesAsync();
        _logger.LogInformation("Worker {WorkerId} set Inactive (logout)", workerId);
    }

    public async Task<bool> UpdateHeartbeatAsync(string workerId)
    {
        var worker = await _context.Workers.FirstOrDefaultAsync(w => w.WorkerId == workerId);
        if (worker == null) return false;
        worker.LastHeartbeat = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<WorkerInfo>> GetActiveWorkersAsync()
    {
        var workers = await _context.Workers
            .OrderByDescending(w => w.Status == "Active")
            .ThenByDescending(w => w.LastHeartbeat)
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

        return workers;
    }

    public async System.Threading.Tasks.Task IncrementTasksProcessedAsync(string workerId)
    {
        var worker = await _context.Workers.FirstOrDefaultAsync(w => w.WorkerId == workerId);
        if (worker != null) { worker.TasksProcessed++; await _context.SaveChangesAsync(); }
    }

    public async System.Threading.Tasks.Task IncrementTasksFailedAsync(string workerId)
    {
        var worker = await _context.Workers.FirstOrDefaultAsync(w => w.WorkerId == workerId);
        if (worker != null) { worker.TasksFailed++; await _context.SaveChangesAsync(); }
    }
}