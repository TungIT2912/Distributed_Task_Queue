using StackExchange.Redis;
using WorkerNode;
using WorkerNode.Services;

var builder = Host.CreateApplicationBuilder(args);

var redisConnectionString = builder.Configuration.GetSection("Redis")["ConnectionString"] ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var options = ConfigurationOptions.Parse(redisConnectionString);
    options.AbortOnConnectFail = false;
    options.ConnectTimeout = 5000;
    options.SyncTimeout = 5000;
    options.ReconnectRetryPolicy = new LinearRetry(500);
    return StackExchange.Redis.ConnectionMultiplexer.Connect(options);
});

builder.Services.AddHttpClient();

builder.Services.AddSingleton<TaskProcessor>();

builder.Services.AddSingleton<CoordinatorClient>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient();
    var configuration = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILogger<CoordinatorClient>>();
    return new CoordinatorClient(httpClient, configuration, logger);
});

// Register worker
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
