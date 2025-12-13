using StackExchange.Redis;
using WorkerNode;
using WorkerNode.Services;

var builder = Host.CreateApplicationBuilder(args);

// Configure Redis
var redisConnectionString = builder.Configuration.GetSection("Redis")["ConnectionString"] ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnectionString));

// Configure HttpClient for Coordinator communication
builder.Services.AddHttpClient<CoordinatorClient>();

// Register services
builder.Services.AddScoped<TaskProcessor>();
builder.Services.AddScoped<CoordinatorClient>();

// Register worker
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
