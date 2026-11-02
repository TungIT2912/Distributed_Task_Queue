using Coordinator.DTOs;
using Coordinator.Services;

namespace Coordinator.Tests;

[TestFixture]
public class AuthServiceTests
{
    private AuthService _auth = null!;

    [SetUp]
    public void Setup()
    {
        _auth = TestHelpers.CreateAuth(TestHelpers.CreateDb());
    }

    [Test]
    public async Task Register_NewUser_ReturnsToken()
    {
        var result = await _auth.RegisterAsync(new RegisterRequest
        {
            Username = "mario88",
            Email = "mario@test.com",
            Password = "password123",
            Role = "User"
        });

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Username, Is.EqualTo("mario88"));
        Assert.That(result.Role, Is.EqualTo("User"));
        Assert.That(result.Token, Is.Not.Empty);
    }

    [Test]
    public async Task Register_DuplicateUsername_ReturnsNull()
    {
        var first = new RegisterRequest
        {
            Username = "mario88",
            Email = "mario@test.com",
            Password = "password123"
        };
        var duplicate = new RegisterRequest
        {
            Username = "mario88",
            Email = "mario2@test.com",
            Password = "password456"
        };

        await _auth.RegisterAsync(first);
        var result = await _auth.RegisterAsync(duplicate);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Register_AdminRole_ReturnsNull()
    {
        var result = await _auth.RegisterAsync(new RegisterRequest
        {
            Username = "boss",
            Email = "boss@test.com",
            Password = "password123",
            Role = "Admin"
        });

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        await _auth.RegisterAsync(new RegisterRequest
        {
            Username = "mario88",
            Email = "mario@test.com",
            Password = "password123"
        });

        var result = await _auth.LoginAsync(new LoginRequest
        {
            Username = "mario88",
            Password = "password123"
        });

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Username, Is.EqualTo("mario88"));
        Assert.That(result.Token, Is.Not.Empty);
    }

    [Test]
    public async Task Login_WrongPassword_ReturnsNull()
    {
        await _auth.RegisterAsync(new RegisterRequest
        {
            Username = "mario88",
            Email = "mario@test.com",
            Password = "password123"
        });

        var result = await _auth.LoginAsync(new LoginRequest
        {
            Username = "mario88",
            Password = "password456"
        });

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Login_UnknownUser_ReturnsNull()
    {
        var result = await _auth.LoginAsync(new LoginRequest
        {
            Username = "ghost",
            Password = "password123"
        });

        Assert.That(result, Is.Null);
    }
}
