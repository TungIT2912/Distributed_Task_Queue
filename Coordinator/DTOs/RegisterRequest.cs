namespace Coordinator.DTOs;

public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    // Allowed: "User", "Worker". Admin is not self-registrable.
    public string Role { get; set; } = "User";
}

