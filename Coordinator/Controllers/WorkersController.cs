using Coordinator.DTOs;
using Coordinator.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coordinator.Controllers;

[ApiController]
[Route("api/workers")]
public class WorkersController : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> RegisterWorker(
        [FromBody] WorkerRegistrationRequest request,
        [FromServices] WorkerService workerService)
    {
        int? userId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out var id))
            {
                userId = id;
            }
        }

        var worker = await workerService.RegisterWorkerAsync(request, userId);
        if (worker == null)
        {
            return BadRequest(new { message = "Failed to register worker" });
        }

        return Ok(new WorkerInfo
        {
            Id = worker.Id,
            WorkerId = worker.WorkerId,
            Status = worker.Status,
            HostAddress = worker.HostAddress,
            Port = worker.Port,
            RegisteredAt = worker.RegisteredAt,
            LastHeartbeat = worker.LastHeartbeat,
            TasksProcessed = worker.TasksProcessed,
            TasksFailed = worker.TasksFailed
        });
    }

    [HttpGet("heartbeat/{workerId}")]
    public async Task<IActionResult> Heartbeat([FromRoute] string workerId, [FromServices] WorkerService workerService)
    {
        var success = await workerService.UpdateHeartbeatAsync(workerId);
        if (!success)
        {
            return NotFound(new { message = "Worker not found" });
        }
        return Ok(new { message = "Heartbeat updated" });
    }

    [Authorize]
    [HttpGet("peers")]
    public async Task<IActionResult> GetActiveWorkers([FromServices] WorkerService workerService)
    {
        var workers = await workerService.GetActiveWorkersAsync();
        return Ok(workers);
    }
}

