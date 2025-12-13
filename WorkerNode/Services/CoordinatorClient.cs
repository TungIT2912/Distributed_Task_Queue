using System.Net.Http.Json;

namespace WorkerNode.Services;

public class CoordinatorClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CoordinatorClient> _logger;
    private readonly string _baseUrl;

    public CoordinatorClient(HttpClient httpClient, IConfiguration configuration, ILogger<CoordinatorClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = configuration.GetSection("Coordinator")["BaseUrl"] ?? "http://localhost:5000";
    }

    public async Task<bool> RegisterWorkerAsync(string workerId, string? hostAddress, int port)
    {
        try
        {
            var request = new
            {
                WorkerId = workerId,
                HostAddress = hostAddress,
                Port = port
            };

            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/api/workers/register", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register worker with coordinator");
            return false;
        }
    }

    public async Task<bool> SendHeartbeatAsync(string workerId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/workers/heartbeat/{workerId}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send heartbeat to coordinator");
            return false;
        }
    }

    public async Task<List<WorkerInfo>> GetActivePeersAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/workers/peers");
            if (response.IsSuccessStatusCode)
            {
                var workers = await response.Content.ReadFromJsonAsync<List<WorkerInfo>>();
                return workers ?? new List<WorkerInfo>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get active peers from coordinator");
        }
        return new List<WorkerInfo>();
    }

    public async Task<bool> UpdateTaskStatusAsync(string taskId, string status, string? result = null, string? errorMessage = null, int? workerId = null)
    {
        try
        {
            var request = new
            {
                Status = status,
                Result = result,
                ErrorMessage = errorMessage,
                WorkerId = workerId
            };

            var response = await _httpClient.PutAsJsonAsync($"{_baseUrl}/api/tasks/{taskId}/status", request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update task status in coordinator");
            return false;
        }
    }
}

public class WorkerInfo
{
    public int Id { get; set; }
    public string WorkerId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? HostAddress { get; set; }
    public int Port { get; set; }
    public DateTime RegisteredAt { get; set; }
    public DateTime? LastHeartbeat { get; set; }
    public int TasksProcessed { get; set; }
    public int TasksFailed { get; set; }
}

