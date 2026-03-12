using StackExchange.Redis;
using WorkerNode;
using WorkerNode.Services;

var builder = Host.CreateApplicationBuilder(args);

// Configure Redis
var redisConnectionString = builder.Configuration.GetSection("Redis")["ConnectionString"] ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnectionString));

// Configure HttpClient for Coordinator communication
builder.Services.AddHttpClient();

// Register services as Singleton (required for BackgroundService/IHostedService)
// TaskProcessor is stateless, so Singleton is safe
builder.Services.AddSingleton<TaskProcessor>();

// CoordinatorClient needs to be Singleton for BackgroundService, but uses HttpClient
// We'll create it using IHttpClientFactory to get a properly configured HttpClient
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
