using System.Net.Http.Json;
using System.Text.Json;
using StackExchange.Redis;
using JobProducer.Models;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var redisConnectionString = configuration.GetSection("Redis")["ConnectionString"] ?? "localhost:6379";
var redis = ConnectionMultiplexer.Connect(redisConnectionString);
var db = redis.GetDatabase();

var streamName = configuration.GetSection("TaskQueue")["StreamName"] ?? "task-queue";
var coordinatorUrl = configuration.GetSection("Coordinator")["BaseUrl"] ?? "http://localhost:5000";

Console.WriteLine("Distributed Task Queue - Job Producer");
Console.WriteLine("====================================");
Console.WriteLine();

// Check if user wants to login
Console.Write("Do you want to login? (y/n): ");
var loginChoice = Console.ReadLine()?.ToLower();

string? token = null;
if (loginChoice == "y")
{
    Console.Write("Username: ");
    var username = Console.ReadLine();
    Console.Write("Password: ");
    var password = ReadPassword();

    using var httpClient = new HttpClient();
    var loginRequest = new { Username = username, Password = password };
    var response = await httpClient.PostAsJsonAsync($"{coordinatorUrl}/api/auth/login", loginRequest);

    if (response.IsSuccessStatusCode)
    {
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
        token = authResponse?.Token;
        Console.WriteLine($"Login successful! Welcome {authResponse?.Username}");
    }
    else
    {
        Console.WriteLine("Login failed. Continuing without authentication...");
    }
}

Console.WriteLine();
Console.WriteLine("Enter task details (or 'exit' to quit):");
Console.WriteLine();

while (true)
{
    Console.Write("Task Type (Compute/DataProcessing/Email/Default): ");
    var taskType = Console.ReadLine() ?? "Default";

    if (taskType.ToLower() == "exit")
        break;

    Console.Write("Priority (0-10, higher = more priority): ");
    if (!int.TryParse(Console.ReadLine(), out var priority))
        priority = 0;

    Console.Write("Payload (JSON or plain text): ");
    var payloadInput = Console.ReadLine() ?? "{}";

    // Create task
    var taskId = Guid.NewGuid().ToString();
    var task = new TaskMessage
    {
        TaskId = taskId,
        TaskType = taskType,
        Payload = payloadInput,
        Priority = priority,
        CreatedAt = DateTime.UtcNow
    };

    var taskJson = JsonSerializer.Serialize(task);

    try
    {
        // Add to Redis Stream
        var entryId = await db.StreamAddAsync(streamName, new NameValueEntry[]
        {
            new("task", taskJson),
            new("taskId", taskId),
            new("taskType", taskType),
            new("priority", priority.ToString()),
            new("createdAt", task.CreatedAt.ToString("O"))
        });

        Console.WriteLine($"Task {taskId} submitted successfully! Entry ID: {entryId}");
        Console.WriteLine();

        // Optionally register task with coordinator
        if (token != null)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                // Note: Coordinator will track tasks when workers process them
                // For now, we just log the submission
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not notify coordinator: {ex.Message}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error submitting task: {ex.Message}");
    }
}

Console.WriteLine("Goodbye!");
redis.Dispose();

static string ReadPassword()
{
    string password = "";
    ConsoleKeyInfo key;
    do
    {
        key = Console.ReadKey(true);
        if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
        {
            password += key.KeyChar;
            Console.Write("*");
        }
        else if (key.Key == ConsoleKey.Backspace && password.Length > 0)
        {
            password = password.Substring(0, password.Length - 1);
            Console.Write("\b \b");
        }
    }
    while (key.Key != ConsoleKey.Enter);
    Console.WriteLine();
    return password;
}

public class TaskMessage
{
    public string TaskId { get; set; } = string.Empty;
    public string TaskType { get; set; } = "Default";
    public string Payload { get; set; } = string.Empty;
    public int Priority { get; set; } = 0;
    public DateTime CreatedAt { get; set; }
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
