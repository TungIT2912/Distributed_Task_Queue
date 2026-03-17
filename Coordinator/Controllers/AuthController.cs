using Coordinator.DTOs;
using Coordinator.Services;
using Microsoft.AspNetCore.Mvc;

namespace Coordinator.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, [FromServices] AuthService authService)
    {
        var result = await authService.RegisterAsync(request);
        if (result == null)
        {
            return BadRequest(new { message = "Username or email already exists" });
        }
        return Ok(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, [FromServices] AuthService authService)
    {
        var result = await authService.LoginAsync(request);
        if (result == null)
        {
            return Unauthorized();
        }
        return Ok(result);
    }
}

