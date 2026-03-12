using System.Net.Http.Json;
using System.Text.Json;
using StackExchange.Redis;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var redisConnectionString = configuration.GetSection("Redis")["ConnectionString"] ?? "localhost:6379";
var redis = ConnectionMultiplexer.Connect(redisConnectionString);
var db = redis.GetDatabase();

var streamName = configuration.GetSection("TaskQueue")["StreamName"] ?? "task-queue";
var coordinatorUrl = configuration.GetSection("Coordinator")["BaseUrl"] ?? "http://localhost:5280";

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
        if (token != null)
        {
            // Preferred path: submit via Coordinator so MySQL tracking is correct
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var submitRes = await httpClient.PostAsJsonAsync($"{coordinatorUrl}/api/tasks/submit", new
            {
                TaskType = taskType,
                Priority = priority,
                Payload = payloadInput
            });

            if (!submitRes.IsSuccessStatusCode)
            {
                var body = await submitRes.Content.ReadAsStringAsync();
                Console.WriteLine($"Error submitting task via coordinator: {(int)submitRes.StatusCode} {submitRes.StatusCode}");
                Console.WriteLine(body);
                continue;
            }

            var submitBody = await submitRes.Content.ReadFromJsonAsync<SubmitTaskResponse>();
            Console.WriteLine($"Task submitted via coordinator! TaskId: {submitBody?.TaskId}, StreamEntryId: {submitBody?.StreamEntryId}");
            Console.WriteLine();
        }
        else
        {
            // Fallback path: direct Redis Streams submit (no MySQL tracking)
            var entryId = await db.StreamAddAsync(streamName, new NameValueEntry[]
            {
                new("task", taskJson),
                new("taskId", taskId),
                new("taskType", taskType),
                new("priority", priority.ToString()),
                new("createdAt", task.CreatedAt.ToString("O"))
            });

            Console.WriteLine($"Task {taskId} submitted to Redis Stream. Entry ID: {entryId}");
            Console.WriteLine();
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

public class SubmitTaskResponse
{
    public string TaskId { get; set; } = string.Empty;
    public string StreamEntryId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
