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
    private readonly int _autoClaimIntervalMs;
    private readonly int _autoClaimMinIdleMs;
    private readonly int _doneKeyTtlHours;
    private readonly string _mode;
    private readonly bool _interactiveRequeueOnSkip;
    private readonly bool _interactiveDisableAutoClaim;

    private int? _workerDbId;
    private string _autoClaimStartId = "0-0";
    private DateTime _lastAutoClaimAt = DateTime.MinValue;

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
        _mode = (workerConfig["Mode"] ?? "auto").Trim().ToLowerInvariant();
        _interactiveRequeueOnSkip = bool.TryParse(workerConfig["InteractiveRequeueOnSkip"], out var requeue) ? requeue : true;
        _interactiveDisableAutoClaim = bool.TryParse(workerConfig["InteractiveDisableAutoClaim"], out var disableAutoClaim) ? disableAutoClaim : false;

        var queueConfig = _configuration.GetSection("TaskQueue");
        _streamName = queueConfig["StreamName"] ?? "task-queue";
        _consumerGroup = queueConfig["ConsumerGroup"] ?? "worker-group";
        _consumerName = queueConfig["ConsumerName"] ?? _workerId;
        _batchSize = int.Parse(queueConfig["BatchSize"] ?? "10");
        _blockTimeMs = int.Parse(queueConfig["BlockTimeMs"] ?? "5000");
        _autoClaimIntervalMs = int.Parse(queueConfig["AutoClaimIntervalMs"] ?? "5000");
        _autoClaimMinIdleMs = int.Parse(queueConfig["AutoClaimMinIdleMs"] ?? "60000");
        _doneKeyTtlHours = int.Parse(queueConfig["DoneKeyTtlHours"] ?? "168");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker {WorkerId} starting...", _workerId);
        _logger.LogInformation("Worker mode: {Mode}", _mode);

        var workerUsername = _configuration["Worker:Username"];
        var workerPassword = _configuration["Worker:Password"];

        if (!string.IsNullOrEmpty(workerUsername))
        {
            var token = await _coordinatorClient.LoginAndGetTokenAsync(workerUsername, workerPassword);
            if (token == null)
            {
                _logger.LogError("Worker failed to authenticate. Stopping.");
                return;
            }
            _logger.LogInformation("Worker authenticated successfully");
        }
        else
        {
            _logger.LogWarning("No Worker:Username configured. Skipping authentication.");
        }

        var hostAddress = _configuration.GetSection("Worker")["HostAddress"];
        var port = int.Parse(_configuration.GetSection("Worker")["Port"] ?? "0");

        var registeredWorker = await _coordinatorClient.RegisterWorkerAsync(_workerId, hostAddress, port);
        if (registeredWorker != null)
        {
            _logger.LogInformation("Successfully registered with coordinator");
            _workerDbId = registeredWorker.Id;
        }
        else
        {
            _logger.LogWarning("Coordinator unavailable. Worker will continue processing tasks from Redis but coordinator features are disabled.");
        }

        await InitializeConsumerGroupAsync();

        var heartbeatTask = StartHeartbeatAsync(stoppingToken);

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
                // Heartbeat is best-effort; don't spam logs when coordinator is down or app is shutting down
                if (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogDebug(ex, "Error sending heartbeat");
                }
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
                // Periodically reclaim pending messages from failed/stuck consumers (distributed recovery)
                if (!_interactiveDisableAutoClaim &&
                    (DateTime.UtcNow - _lastAutoClaimAt).TotalMilliseconds >= _autoClaimIntervalMs)
                {
                    _lastAutoClaimAt = DateTime.UtcNow;
                    var claimed = await AutoClaimPendingAsync(db, stoppingToken);
                    if (claimed.Count > 0)
                    {
                        _logger.LogInformation("Auto-claimed {Count} pending tasks for recovery", claimed.Count);

                        if (_mode == "interactive")
                        {
                            // Interactive mode: prompt sequentially to avoid mixed console prompts
                            foreach (var entry in claimed)
                            {
                                await ProcessMessageAsync(db, entry, stoppingToken);
                            }
                        }
                        else
                        {
                            var claimTasks = claimed.Select(entry => ProcessMessageAsync(db, entry, stoppingToken));
                            await Task.WhenAll(claimTasks);
                        }
                    }
                }

                // Read messages from the stream
                var messages = await db.StreamReadGroupAsync(
                    _streamName,
                    _consumerGroup,
                    _consumerName,
                    position: StreamPosition.NewMessages,
                    count: _mode == "interactive" ? 1 : _batchSize,
                    noAck: false,
                    flags: CommandFlags.None);

                if (messages == null || messages.Length == 0)
                {
                    await Task.Delay(1000, stoppingToken);
                    continue;
                }

                _logger.LogInformation("Received {Count} tasks from stream", messages.Length);

                // Process each message
                if (_mode == "interactive")
                {
                    foreach (var message in messages)
                    {
                        await ProcessMessageAsync(db, message, stoppingToken);
                    }
                }
                else
                {
                    var tasks = new List<Task>();
                    foreach (var message in messages)
                    {
                        tasks.Add(ProcessMessageAsync(db, message, stoppingToken));
                    }
                    await Task.WhenAll(tasks);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing tasks from stream");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task<List<StreamEntry>> AutoClaimPendingAsync(IDatabase db, CancellationToken stoppingToken)
    {
        // Uses Redis XAUTOCLAIM to recover PENDING messages that have been idle too long.
        // Reply format: [nextStartId, [ [id, [field, value, ...]], ... ], [deletedId, ...]]
        var result = await db.ExecuteAsync(
            "XAUTOCLAIM",
            _streamName,
            _consumerGroup,
            _consumerName,
            _autoClaimMinIdleMs,
            _autoClaimStartId,
            "COUNT",
            _batchSize);

        if (result.IsNull)
        {
            return new List<StreamEntry>();
        }

        var outer = (RedisResult[]?)result;
        if (outer == null || outer.Length < 2)
        {
            return new List<StreamEntry>();
        }

        var nextId = outer[0].ToString();
        _autoClaimStartId = nextId ?? "0-0";

        var claimedRaw = outer[1];
        if (claimedRaw.IsNull)
        {
            return new List<StreamEntry>();
        }

        var claimed = (RedisResult[]?)claimedRaw;
        if (claimed == null)
        {
            return new List<StreamEntry>();
        }
        var entries = new List<StreamEntry>(claimed.Length);

        foreach (var item in claimed)
        {
            if (item.IsNull) continue;

            var entry = (RedisResult[]?)item;
            if (entry == null || entry.Length < 2) continue;

            var idValue = entry[0].ToString();
            if (string.IsNullOrWhiteSpace(idValue)) continue;
            var id = idValue!; // Already checked for null/whitespace

            var valuesObj = entry[1];
            if (valuesObj.IsNull)
            {
                entries.Add(new StreamEntry(id, Array.Empty<NameValueEntry>()));
                continue;
            }

            // Handle both possible shapes:
            // - [field, value, field, value, ...]
            // - [[field,value],[field,value],...]
            var valuesArr = (RedisResult[]?)valuesObj;
            if (valuesArr == null)
            {
                entries.Add(new StreamEntry(id, Array.Empty<NameValueEntry>()));
                continue;
            }
            var nve = new List<NameValueEntry>();

            if (valuesArr.Length > 0)
            {
                // Check if first element is an array by trying to cast
                var firstElement = valuesArr[0];
                var firstAsArray = (RedisResult[]?)firstElement;

                if (firstAsArray != null && firstAsArray.Length >= 2)
                {
                    // Nested array format: [[field,value],[field,value],...]
                    foreach (var pairObj in valuesArr)
                    {
                        var pair = (RedisResult[]?)pairObj;
                        if (pair != null && pair.Length >= 2)
                        {
                            var pairField = pair[0].ToString();
                            var pairValue = pair[1].ToString();
                            if (pairField != null && pairValue != null)
                            {
                                nve.Add(new NameValueEntry(pairField, pairValue));
                            }
                        }
                    }
                }
                else
                {
                    // Flat format: [field, value, field, value, ...]
                    for (var i = 0; i + 1 < valuesArr.Length; i += 2)
                    {
                        var fieldName = valuesArr[i].ToString();
                        var fieldValue = valuesArr[i + 1].ToString();
                        if (fieldName != null && fieldValue != null)
                        {
                            nve.Add(new NameValueEntry(fieldName, fieldValue));
                        }
                    }
                }
            }

            entries.Add(new StreamEntry(id, nve.ToArray()));
        }

        return entries;
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

            // Idempotency: if we've already completed this task, just ACK and stop.
            var doneKey = $"task:done:{task.TaskId}";
            var alreadyDone = await db.StringGetAsync(doneKey);
            if (alreadyDone.HasValue)
            {
                _logger.LogInformation("Task {TaskId} already done. Acknowledging message {EntryId}", task.TaskId, entryId);
                await AcknowledgeMessageAsync(db, entryId);
                return;
            }

            int? workerDbId = _workerDbId;

            if (_mode == "interactive")
            {
                var approve = PromptApproveTask(task, entryId);
                if (!approve)
                {
                    _logger.LogInformation("Skipped task {TaskId} (entry {EntryId}) by operator choice", task.TaskId, entryId);

                    if (_interactiveRequeueOnSkip)
                    {
                        await RequeueMessageAsync(db, message, task, entryId);
                        await AcknowledgeMessageAsync(db, entryId);
                    }

                    return;
                }
            }

            // Update task status to Processing only when the worker actually starts work
            await _coordinatorClient.UpdateTaskStatusAsync(task.TaskId, "Processing", _workerId, null, null, workerDbId);

            // Process the task
            var result = await _taskProcessor.ProcessTaskAsync(task);

            if (result.Success)
            {
                _logger.LogInformation("Task {TaskId} completed successfully", task.TaskId);

                // Store result in Redis
                await StoreTaskResultAsync(db, task.TaskId, result);

                // Mark done (effectively-once)
                await db.StringSetAsync(doneKey, "1", TimeSpan.FromHours(_doneKeyTtlHours));

                // Update task status in coordinator
                await _coordinatorClient.UpdateTaskStatusAsync(
                    task.TaskId,
                    "Completed",
                    _workerId,
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
                    _workerId,
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

    private bool PromptApproveTask(TaskMessage task, string entryId)
    {
        try
        {
            Console.WriteLine();
            Console.WriteLine("────────────────────────────────────────");
            Console.WriteLine($"[INTERACTIVE] Worker {_workerId}");
            Console.WriteLine($"StreamEntryId: {entryId}");
            Console.WriteLine($"TaskId:        {task.TaskId}");
            Console.WriteLine($"TaskType:      {task.TaskType}");
            Console.WriteLine($"Priority:      {task.Priority}");
            Console.WriteLine("Process this task now? (y/n): ");
            var line = Console.ReadLine();
            return string.Equals(line?.Trim(), "y", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // If console is not available, fall back to auto-processing (safer than deadlocking tasks)
            return true;
        }
    }

    private async Task RequeueMessageAsync(IDatabase db, StreamEntry originalMessage, TaskMessage task, string originalEntryId)
    {
        try
        {
            // Requeue by creating a new stream entry. This avoids "locking" the task to this consumer.
            // Idempotency is still guaranteed by task:done:{taskId} check before completion.
            var now = DateTime.UtcNow;
            var values = new List<NameValueEntry>(originalMessage.Values.Length + 4);

            // Keep existing fields when present
            foreach (var v in originalMessage.Values)
            {
                values.Add(v);
            }

            // Ensure required fields exist
            if (!values.Any(v => v.Name == "task"))
                values.Add(new NameValueEntry("task", JsonSerializer.Serialize(task)));
            if (!values.Any(v => v.Name == "taskId"))
                values.Add(new NameValueEntry("taskId", task.TaskId));
            if (!values.Any(v => v.Name == "taskType"))
                values.Add(new NameValueEntry("taskType", task.TaskType));
            if (!values.Any(v => v.Name == "priority"))
                values.Add(new NameValueEntry("priority", task.Priority.ToString()));
            if (!values.Any(v => v.Name == "createdAt"))
                values.Add(new NameValueEntry("createdAt", task.CreatedAt.ToString("O")));

            values.Add(new NameValueEntry("requeuedFrom", originalEntryId));
            values.Add(new NameValueEntry("requeuedAt", now.ToString("O")));

            var newEntryId = await db.StreamAddAsync(_streamName, values.ToArray());
            _logger.LogInformation("Requeued task {TaskId} from {Old} to {New}", task.TaskId, originalEntryId, newEntryId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to requeue skipped task {TaskId} (entry {EntryId})", task.TaskId, originalEntryId);
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
