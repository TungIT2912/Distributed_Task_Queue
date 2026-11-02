using Coordinator.Data;
using Coordinator.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Coordinator.Tests;

internal static class TestHelpers
{
    public static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    public static IConfiguration JwtConfig()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:SecretKey"] = "YourSuperSecretKeyThatShouldBeAtLeast32CharactersLong!",
                ["JwtSettings:Issuer"] = "DistributedTaskQueue",
                ["JwtSettings:Audience"] = "DistributedTaskQueue",
                ["JwtSettings:ExpirationMinutes"] = "60"
            })
            .Build();
    }

    public static AuthService CreateAuth(ApplicationDbContext db)
    {
        return new AuthService(db, JwtConfig(), NullLogger<AuthService>.Instance);
    }

    public static TaskService CreateTasks(ApplicationDbContext db)
    {
        return new TaskService(db, NullLogger<TaskService>.Instance);
    }

    public static WorkerService CreateWorkers(ApplicationDbContext db)
    {
        return new WorkerService(db, NullLogger<WorkerService>.Instance);
    }
}
