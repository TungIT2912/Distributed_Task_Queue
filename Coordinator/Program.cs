using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Coordinator.Data;
using Coordinator.Services;
using Coordinator.Models;
using Coordinator.DTOs;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Distributed Task Queue Coordinator API", Version = "v1" });
    
    // Add JWT authentication to Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Add Entity Framework
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 21))
    ));

// Add Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var connectionString = builder.Configuration.GetSection("Redis")["ConnectionString"] ?? "localhost:6379";
    return StackExchange.Redis.ConnectionMultiplexer.Connect(connectionString);
});

// Add JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? "YourSuperSecretKeyThatShouldBeAtLeast32CharactersLong!";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"] ?? "DistributedTaskQueue",
        ValidAudience = jwtSettings["Audience"] ?? "DistributedTaskQueue",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

builder.Services.AddAuthorization();

// Add custom services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<WorkerService>();
builder.Services.AddScoped<TaskService>();

// Add background services
builder.Services.AddHostedService<Services.StaleTaskMonitor>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.EnsureCreated();
    
    // Create default admin user if not exists
    if (!dbContext.Users.Any())
    {
        var adminUser = new User
        {
            Username = "admin",
            Email = "admin@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            Role = "Admin"
        };
        dbContext.Users.Add(adminUser);
        dbContext.SaveChanges();
    }
}

// Authentication endpoints
app.MapPost("/api/auth/register", async (RegisterRequest request, AuthService authService) =>
{
    var result = await authService.RegisterAsync(request);
    if (result == null)
    {
        return Results.BadRequest(new { message = "Username or email already exists" });
    }
    return Results.Ok(result);
})
.WithName("Register")
.WithTags("Authentication");

app.MapPost("/api/auth/login", async (LoginRequest request, AuthService authService) =>
{
    var result = await authService.LoginAsync(request);
    if (result == null)
    {
        return Results.Unauthorized();
    }
    return Results.Ok(result);
})
.WithName("Login")
.WithTags("Authentication");

// Worker registration and monitoring endpoints
app.MapPost("/api/workers/register", async (WorkerRegistrationRequest request, WorkerService workerService, HttpContext context) =>
{
    int? userId = null;
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var userIdClaim = context.User.FindFirst("UserId")?.Value;
        if (int.TryParse(userIdClaim, out var id))
        {
            userId = id;
        }
    }

    var worker = await workerService.RegisterWorkerAsync(request, userId);
    if (worker == null)
    {
        return Results.BadRequest(new { message = "Failed to register worker" });
    }
    return Results.Ok(new WorkerInfo
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
})
.WithName("RegisterWorker")
.WithTags("Workers")
.RequireAuthorization();

app.MapGet("/api/workers/heartbeat/{workerId}", async (string workerId, WorkerService workerService) =>
{
    var success = await workerService.UpdateHeartbeatAsync(workerId);
    if (!success)
    {
        return Results.NotFound(new { message = "Worker not found" });
    }
    return Results.Ok(new { message = "Heartbeat updated" });
})
.WithName("UpdateHeartbeat")
.WithTags("Workers");

app.MapGet("/api/workers/peers", async (WorkerService workerService) =>
{
    var workers = await workerService.GetActiveWorkersAsync();
    return Results.Ok(workers);
})
.WithName("GetActiveWorkers")
.WithTags("Workers")
.RequireAuthorization();

// Task monitoring endpoints
app.MapGet("/api/tasks", async (TaskService taskService, HttpContext context, string? status = null, int limit = 100) =>
{
    int? userId = null;
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var userIdClaim = context.User.FindFirst("UserId")?.Value;
        if (int.TryParse(userIdClaim, out var id))
        {
            userId = id;
        }
    }

    var tasks = await taskService.GetTasksAsync(userId, status, limit);
    return Results.Ok(tasks);
})
.WithName("GetTasks")
.WithTags("Tasks")
.RequireAuthorization();

app.MapGet("/api/tasks/{taskId}", async (string taskId, TaskService taskService, HttpContext context) =>
{
    int? userId = null;
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var userIdClaim = context.User.FindFirst("UserId")?.Value;
        if (int.TryParse(userIdClaim, out var id))
        {
            userId = id;
        }
    }

    var tasks = await taskService.GetTasksAsync(userId);
    var task = tasks.FirstOrDefault(t => t.TaskId == taskId);
    
    if (task == null)
    {
        return Results.NotFound();
    }
    
    return Results.Ok(task);
})
.WithName("GetTask")
.WithTags("Tasks")
.RequireAuthorization();

app.MapPut("/api/tasks/{taskId}/status", async (string taskId, UpdateTaskStatusRequest request, TaskService taskService, ApplicationDbContext dbContext) =>
{
    var task = await taskService.UpdateTaskStatusAsync(taskId, request.Status, request.Result, request.ErrorMessage, request.WorkerId);
    
    if (task == null)
    {
        return Results.NotFound(new { message = "Task not found" });
    }
    
    return Results.Ok(new TaskInfo
    {
        Id = task.Id,
        TaskId = task.TaskId,
        Status = task.Status,
        TaskType = task.TaskType,
        Priority = task.Priority,
        CreatedAt = task.CreatedAt,
        StartedAt = task.StartedAt,
        CompletedAt = task.CompletedAt,
        Result = task.Result,
        ErrorMessage = task.ErrorMessage,
        RetryCount = task.RetryCount,
        WorkerId = task.Worker?.WorkerId
    });
})
.WithName("UpdateTaskStatus")
.WithTags("Tasks");

app.Run();

record UpdateTaskStatusRequest(string Status, string? Result = null, string? ErrorMessage = null, int? WorkerId = null);
