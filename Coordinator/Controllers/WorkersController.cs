using Coordinator.DTOs;
using Coordinator.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coordinator.Controllers;

[ApiController]
[Route("api/workers")]
public class WorkersController : ControllerBase
{
    private readonly WorkerService _workerService;

    public WorkersController(WorkerService workerService)
    {
        _workerService = workerService;
    }

    // Login / Register → sets status Active
    [HttpPost("register")]
    [Authorize(Roles = "Worker,Admin")]
    public async Task<IActionResult> RegisterWorker([FromBody] RegisterWorkerRequest request)
    {
        var userIdClaim = User.FindFirst("UserId")?.Value;
        var userId = userIdClaim != null ? int.Parse(userIdClaim) : (int?)null;

        var worker = await _workerService.RegisterWorkerAsync(new WorkerRegistrationRequest
        {
            WorkerId = request.WorkerId,
            HostAddress = request.HostAddress,
            Port = request.Port
        }, userId);

        return Ok(worker);
    }

    // Explicit logout → sets status Inactive immediately
    [HttpPost("{workerId}/logout")]
    [Authorize]
    public async Task<IActionResult> WorkerLogout([FromRoute] string workerId)
    {
        await _workerService.SetInactiveAsync(workerId);
        return Ok(new { message = "Worker set to inactive" });
    }

    // Heartbeat — only used during active task processing
    [HttpGet("heartbeat/{workerId}")]
    public async Task<IActionResult> Heartbeat(
        [FromRoute] string workerId,
        [FromServices] WorkerService workerService)
    {
        var success = await workerService.UpdateHeartbeatAsync(workerId);
        if (!success) return NotFound(new { message = "Worker not found" });
        return Ok(new { message = "Heartbeat updated" });
    }

    [Authorize]
    [HttpGet("peers")]
    public async Task<IActionResult> GetActiveWorkers(
        [FromServices] WorkerService workerService)
    {
        var workers = await workerService.GetActiveWorkersAsync();
        return Ok(workers);
    }
}