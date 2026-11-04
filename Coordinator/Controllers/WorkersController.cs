using Coordinator.DTOs;
using Coordinator.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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

        var worker = await _workerService.RegisterWorkerAsync(request, userId);

        return Ok(worker);
    }

    // Explicit logout → sets status Inactive immediately
    [HttpPost("{workerId}/logout")]
    [Authorize]
    public async Task<IActionResult> WorkerLogout([FromRoute] string workerId)
    {
        var userIdClaim = User.FindFirst("UserId")?.Value;

        if (!int.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var success = await _workerService.SetInactiveIfOwnedAsync(
            workerId, userId);

        if (!success)
            return NotFound(new { message = "Worker not found or not owned by user" });

        return Ok(new { message = "Worker offline" });
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