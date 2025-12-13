# Distributed Task Queue System

A fault-tolerant, decentralized distributed task scheduling system built with .NET 10, Redis Streams, and MySQL.

## Architecture

This system implements a distributed task queue where multiple worker nodes cooperate to process jobs from a shared queue in a fault-tolerant, decentralized manner.

### Components

1. **Coordinator** - ASP.NET Core Web API that provides:
   - User authentication and authorization (JWT)
   - Worker node registration
   - Active peer listing for distributed coordination
   - Task monitoring and analytics
   - Stale task reassignment

2. **Worker Nodes** - Background services that:
   - Register with the coordinator
   - Process tasks from Redis Streams
   - Send heartbeats to maintain active status
   - Handle task failures and retries
   - Operate in a peer-to-peer manner

3. **Job Producer** - Console application that:
   - Submits tasks to Redis Streams
   - Supports authentication for user tracking
   - Allows interactive task creation

### Key Features

- **Fault Tolerance**: If a node fails mid-task, uncompleted jobs are automatically reassigned
- **Exactly-Once Processing**: Each job is processed exactly once using Redis Streams consumer groups
- **Decentralized Coordination**: Workers operate peer-to-peer after initial registration
- **Authentication & Authorization**: JWT-based authentication with user management
- **Database Persistence**: MySQL database stores users, workers, and task metadata
- **Session Management**: Distributed session tracking across nodes

## Prerequisites

- .NET 10 SDK
- MySQL (via Laragon or standalone)
- Redis Server
- Docker and Docker Compose (optional, for containerized deployment)

## Database Setup (Laragon)

1. Start Laragon and ensure MySQL is running
2. Create a database named `DistributedTaskQueue` (or update connection string)
3. The application will automatically create tables on first run using Entity Framework Core

Default connection string (update in `appsettings.json`):
```
Server=localhost;Port=3306;Database=DistributedTaskQueue;User=root;Password=;
```

## Configuration

### Coordinator (`Coordinator/appsettings.json`)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=DistributedTaskQueue;User=root;Password=;"
  },
  "JwtSettings": {
    "SecretKey": "YourSuperSecretKeyThatShouldBeAtLeast32CharactersLongForProductionUse!",
    "Issuer": "DistributedTaskQueue",
    "Audience": "DistributedTaskQueue",
    "ExpirationMinutes": "60"
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "TaskQueue": {
    "StreamName": "task-queue",
    "ConsumerGroup": "worker-group",
    "TaskTimeoutMinutes": "30"
  }
}
```

### Worker Node (`WorkerNode/appsettings.json`)

```json
{
  "Worker": {
    "WorkerId": "worker-1",
    "HostAddress": "localhost",
    "Port": 8080
  },
  "Coordinator": {
    "BaseUrl": "http://localhost:5000"
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "TaskQueue": {
    "StreamName": "task-queue",
    "ConsumerGroup": "worker-group",
    "ConsumerName": "worker-1",
    "BatchSize": 10,
    "BlockTimeMs": 5000
  }
}
```

## Running the System

### Option 1: Local Development

1. **Start Redis** (if not running):
   ```bash
   redis-server
   ```

2. **Start the Coordinator**:
   ```bash
   cd Coordinator
   dotnet run
   ```
   The API will be available at `http://localhost:5000` (or check `launchSettings.json`)

3. **Start Worker Nodes** (in separate terminals):
   ```bash
   cd WorkerNode
   dotnet run
   ```
   Repeat for multiple worker instances (update `WorkerId` in `appsettings.json`)

4. **Run Job Producer**:
   ```bash
   cd JobProducer
   dotnet run
   ```

### Option 2: Docker Compose

```bash
docker-compose up -d
```

This will start:
- MySQL database
- Redis server
- Coordinator API
- Two worker nodes

## API Endpoints

### Authentication

- `POST /api/auth/register` - Register a new user
- `POST /api/auth/login` - Login and get JWT token

### Workers

- `POST /api/workers/register` - Register a worker node (requires auth)
- `GET /api/workers/heartbeat/{workerId}` - Update worker heartbeat
- `GET /api/workers/peers` - Get list of active worker peers (requires auth)

### Tasks

- `GET /api/tasks` - Get tasks (requires auth, filters by user)
- `GET /api/tasks/{taskId}` - Get specific task (requires auth)

## Default Credentials

- Username: `admin`
- Password: `admin123`

**Note**: Change these in production!

## Task Types

The system supports different task types:
- `Compute` - Computational tasks
- `DataProcessing` - Data processing tasks
- `Email` - Email sending tasks
- `Default` - Generic tasks

## Distributed System Principles

This implementation demonstrates:

1. **Consistency**: Tasks are tracked in the database and Redis Streams
2. **Reliability**: Exactly-once processing via Redis Streams consumer groups
3. **Fault Tolerance**: Stale task monitoring and automatic reassignment
4. **Coordination**: Decentralized peer-to-peer operation after registration
5. **CAP Theorem**: Prioritizes Availability and Partition tolerance (AP system)

## Monitoring

- Worker heartbeats are sent every 30 seconds
- Stale tasks (processing > 30 minutes) are automatically reassigned
- Workers inactive for > 5 minutes are marked as inactive
- Task results are stored in Redis with 24-hour TTL

## Development Notes

- The system uses Entity Framework Core with MySQL (Pomelo provider)
- JWT tokens are used for authentication
- Redis Streams provide the distributed queue mechanism
- Worker nodes operate independently after registration
- Session information is stored in the database and tracked per user

## License

This is an educational project demonstrating distributed systems principles.
