using System.Text.Json;
using StackExchange.Redis;
using WorkerNode.Models;
using WorkerNode.Services;

namespace WorkerNode;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IConnectionMultiplexer _redis;
    private readonly TaskProcessor _taskProcessor;
    private readonly CoordinatorClient _coordinatorClient;
    private readonly string _workerId;
    private readonly string _streamName;
    private readonly string _consumerGroup;
    private readonly string _consumerName;
    private readonly int _batchSize;
    private readonly int _blockTimeMs;

    public Worker(
        ILogger<Worker> logger,
        IConfiguration configuration,
        IConnectionMultiplexer redis,
        TaskProcessor taskProcessor,
        CoordinatorClient coordinatorClient)
    {
        _logger = logger;
        _configuration = configuration;
        _redis = redis;
        _taskProcessor = taskProcessor;
        _coordinatorClient = coordinatorClient;

        var workerConfig = _configuration.GetSection("Worker");
        _workerId = workerConfig["WorkerId"] ?? $"worker-{Guid.NewGuid()}";
        
        var queueConfig = _configuration.GetSection("TaskQueue");
        _streamName = queueConfig["StreamName"] ?? "task-queue";
        _consumerGroup = queueConfig["ConsumerGroup"] ?? "worker-group";
        _consumerName = queueConfig["ConsumerName"] ?? _workerId;
        _batchSize = int.Parse(queueConfig["BatchSize"] ?? "10");
        _blockTimeMs = int.Parse(queueConfig["BlockTimeMs"] ?? "5000");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker {WorkerId} starting...", _workerId);

        // Register with coordinator
        var hostAddress = _configuration.GetSection("Worker")["HostAddress"];
        var port = int.Parse(_configuration.GetSection("Worker")["Port"] ?? "0");
        
        await _coordinatorClient.RegisterWorkerAsync(_workerId, hostAddress, port);

        // Initialize Redis Stream consumer group
        await InitializeConsumerGroupAsync();

        // Start heartbeat task
        var heartbeatTask = StartHeartbeatAsync(stoppingToken);

        // Start processing tasks
        await ProcessTasksAsync(stoppingToken);

        await heartbeatTask;
    }

    private async Task InitializeConsumerGroupAsync()
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.StreamCreateConsumerGroupAsync(_streamName, _consumerGroup, StreamPosition.NewMessages);
            _logger.LogInformation("Consumer group {Group} initialized", _consumerGroup);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            _logger.LogInformation("Consumer group {Group} already exists", _consumerGroup);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize consumer group");
        }
    }

    private async Task StartHeartbeatAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _coordinatorClient.SendHeartbeatAsync(_workerId);
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending heartbeat");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task ProcessTasksAsync(CancellationToken stoppingToken)
    {
        var db = _redis.GetDatabase();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Read messages from the stream
                var messages = await db.StreamReadGroupAsync(
                    _streamName,
                    _consumerGroup,
                    _consumerName,
                    position: StreamPosition.NewMessages,
                    count: _batchSize,
                    noAck: false,
                    flags: CommandFlags.None);

                if (messages == null || messages.Length == 0)
                {
                    await Task.Delay(1000, stoppingToken);
                    continue;
                }

                _logger.LogInformation("Received {Count} tasks from stream", messages.Length);

                // Process each message
                var tasks = new List<Task>();

                foreach (var message in messages)
                {
                    tasks.Add(ProcessMessageAsync(db, message, stoppingToken));
                }

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing tasks from stream");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task ProcessMessageAsync(IDatabase db, StreamEntry message, CancellationToken stoppingToken)
    {
        var entryId = message.Id.ToString();
        _logger.LogInformation("Processing message {EntryId}", entryId);

        try
        {
            // Parse task message
            var taskJson = message.Values.FirstOrDefault(v => v.Name == "task").Value.ToString();
            if (string.IsNullOrEmpty(taskJson))
            {
                _logger.LogWarning("Empty task payload for entry {EntryId}", entryId);
                await AcknowledgeMessageAsync(db, entryId);
                return;
            }

            var task = JsonSerializer.Deserialize<TaskMessage>(taskJson);
            if (task == null)
            {
                _logger.LogWarning("Failed to deserialize task for entry {EntryId}", entryId);
                await AcknowledgeMessageAsync(db, entryId);
                return;
            }

            // Get worker ID from database for status update
            var workerInfo = await _coordinatorClient.GetActivePeersAsync();
            var currentWorker = workerInfo.FirstOrDefault(w => w.WorkerId == _workerId);
            int? workerDbId = currentWorker?.Id;

            // Update task status to Processing
            await _coordinatorClient.UpdateTaskStatusAsync(
                task.TaskId, 
                "Processing", 
                null, 
                null, 
                workerDbId);

            // Process the task
            var result = await _taskProcessor.ProcessTaskAsync(task);

            if (result.Success)
            {
                _logger.LogInformation("Task {TaskId} completed successfully", task.TaskId);
                
                // Store result in Redis
                await StoreTaskResultAsync(db, task.TaskId, result);
                
                // Update task status in coordinator
                await _coordinatorClient.UpdateTaskStatusAsync(
                    task.TaskId, 
                    "Completed", 
                    result.Result, 
                    null, 
                    workerDbId);
            }
            else
            {
                _logger.LogWarning("Task {TaskId} failed: {Error}", task.TaskId, result.ErrorMessage);
                
                // Handle retry logic
                await HandleTaskFailureAsync(db, task, result);
                
                // Update task status in coordinator
                await _coordinatorClient.UpdateTaskStatusAsync(
                    task.TaskId, 
                    "Failed", 
                    null, 
                    result.ErrorMessage, 
                    workerDbId);
            }

            // Acknowledge the message
            await AcknowledgeMessageAsync(db, entryId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message {EntryId}", entryId);
            
            // Don't acknowledge on error - let it be retried
            // The message will be claimed by another consumer after PENDING timeout
        }
    }

    private async Task AcknowledgeMessageAsync(IDatabase db, string entryId)
    {
        try
        {
            await db.StreamAcknowledgeAsync(_streamName, _consumerGroup, entryId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acknowledge message {EntryId}", entryId);
        }
    }

    private async Task StoreTaskResultAsync(IDatabase db, string taskId, TaskResult result)
    {
        try
        {
            var resultKey = $"task:result:{taskId}";
            var resultJson = JsonSerializer.Serialize(result);
            await db.StringSetAsync(resultKey, resultJson, TimeSpan.FromHours(24));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store result for task {TaskId}", taskId);
        }
    }

    private async Task HandleTaskFailureAsync(IDatabase db, TaskMessage task, TaskResult result)
    {
        // For now, just log the failure
        // In a production system, you might want to:
        // 1. Check retry count
        // 2. Move to a dead letter queue
        // 3. Notify the coordinator
        _logger.LogWarning("Task {TaskId} failed and will be retried", task.TaskId);
    }

    public override void Dispose()
    {
        _redis?.Dispose();
        base.Dispose();
    }
}
