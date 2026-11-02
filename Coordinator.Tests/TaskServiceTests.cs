using Coordinator.Data;
using Coordinator.Models;
using Coordinator.Services;
using Task = System.Threading.Tasks.Task;

namespace Coordinator.Tests;

[TestFixture]
public class TaskServiceTests
{
    private ApplicationDbContext _db = null!;
    private TaskService _tasks = null!;

    [SetUp]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _tasks = TestHelpers.CreateTasks(_db);
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
    }

    [Test]
    public async Task CreateTask_SetsPendingStatus()
    {
        var task = await _tasks.CreateTaskAsync("t-1", "{\"n\":1}", "Default", 5, userId: 1, streamEntryId: "0-1");

        Assert.That(task.TaskId, Is.EqualTo("t-1"));
        Assert.That(task.Status, Is.EqualTo("Pending"));
        Assert.That(task.Payload, Is.EqualTo("{\"n\":1}"));
        Assert.That(task.Priority, Is.EqualTo(5));
        Assert.That(task.UserId, Is.EqualTo(1));
    }

    [Test]
    public async Task UpdateStatus_UnknownTask_ReturnsNull()
    {
        var result = await _tasks.UpdateTaskStatusAsync("missing", "Processing");

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task UpdateStatus_Processing_SetsStartedAtAndWorker()
    {
        await _tasks.CreateTaskAsync("t-2", "{}", "Default", 0, userId: 1);
        _db.Workers.Add(new Worker { WorkerId = "w-1", Status = "Active" });
        await _db.SaveChangesAsync();

        var result = await _tasks.UpdateTaskStatusAsync("t-2", "Processing", workerKey: "w-1");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Status, Is.EqualTo("Processing"));
        Assert.That(result.StartedAt, Is.Not.Null);
        Assert.That(result.WorkerId, Is.Not.Null);
    }

    [Test]
    public async Task UpdateStatus_Completed_StoresResult()
    {
        await _tasks.CreateTaskAsync("t-3", "{}", "Default", 0, userId: 1);

        var result = await _tasks.UpdateTaskStatusAsync("t-3", "Completed", result: "ok");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Status, Is.EqualTo("Completed"));
        Assert.That(result.Result, Is.EqualTo("ok"));
        Assert.That(result.CompletedAt, Is.Not.Null);
    }

    [Test]
    public async Task UpdateStatus_Failed_IncrementsRetryCount()
    {
        await _tasks.CreateTaskAsync("t-4", "{}", "Default", 0, userId: 1);

        var result = await _tasks.UpdateTaskStatusAsync("t-4", "Failed", errorMessage: "boom");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Status, Is.EqualTo("Failed"));
        Assert.That(result.ErrorMessage, Is.EqualTo("boom"));
        Assert.That(result.RetryCount, Is.EqualTo(1));
    }

    [Test]
    public async Task GetTasks_FiltersByUser()
    {
        await _tasks.CreateTaskAsync("u1-a", "{}", "Default", 0, userId: 1);
        await _tasks.CreateTaskAsync("u2-a", "{}", "Default", 0, userId: 2);

        var userOne = await _tasks.GetTasksAsync(userId: 1);

        Assert.That(userOne, Has.Count.EqualTo(1));
        Assert.That(userOne[0].TaskId, Is.EqualTo("u1-a"));
    }

    [Test]
    public async Task ReassignStaleTasks_MarksTimedOutProcessingAsReassigned()
    {
        var task = await _tasks.CreateTaskAsync("stale-1", "{}", "Default", 0, userId: 1);
        task.Status = "Processing";
        task.StartedAt = DateTime.UtcNow.AddMinutes(-30);
        await _db.SaveChangesAsync();

        await _tasks.ReassignStaleTasksAsync(TimeSpan.FromMinutes(5));

        var updated = await _tasks.GetTaskByTaskIdAsync("stale-1");
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.Status, Is.EqualTo("Reassigned"));
        Assert.That(updated.RetryCount, Is.EqualTo(1));
        Assert.That(updated.WorkerId, Is.Null);
    }
}
