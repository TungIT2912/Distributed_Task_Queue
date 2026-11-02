using Coordinator.Data;
using Coordinator.DTOs;
using Coordinator.Services;

namespace Coordinator.Tests;

[TestFixture]
public class WorkerServiceTests
{
    private ApplicationDbContext _db = null!;
    private WorkerService _workers = null!;

    [SetUp]
    public void Setup()
    {
        _db = TestHelpers.CreateDb();
        _workers = TestHelpers.CreateWorkers(_db);
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
    }

    [Test]
    public async Task RegisterWorker_NewWorker_IsActive()
    {
        var worker = await _workers.RegisterWorkerAsync(new RegisterWorkerRequest
        {
            WorkerId = "w-1",
            HostAddress = "127.0.0.1",
            Port = 5001
        }, userId: 9);

        Assert.That(worker, Is.Not.Null);
        Assert.That(worker!.Status, Is.EqualTo("Active"));
        Assert.That(worker.UserId, Is.EqualTo(9));
        Assert.That(worker.Port, Is.EqualTo(5001));
    }

    [Test]
    public async Task RegisterWorker_ExistingId_UpdatesHostAndStaysActive()
    {
        await _workers.RegisterWorkerAsync(new RegisterWorkerRequest
        {
            WorkerId = "w-1",
            HostAddress = "old-host",
            Port = 1
        });

        var updated = await _workers.RegisterWorkerAsync(new RegisterWorkerRequest
        {
            WorkerId = "w-1",
            HostAddress = "new-host",
            Port = 2
        });

        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.HostAddress, Is.EqualTo("new-host"));
        Assert.That(updated.Port, Is.EqualTo(2));
        Assert.That(updated.Status, Is.EqualTo("Active"));
        Assert.That(_db.Workers.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task SetInactive_MarksWorkerInactive()
    {
        await _workers.RegisterWorkerAsync(new RegisterWorkerRequest { WorkerId = "w-1" });

        await _workers.SetInactiveAsync("w-1");

        var listed = await _workers.GetActiveWorkersAsync();
        Assert.That(listed.Single().Status, Is.EqualTo("Inactive"));
    }

    [Test]
    public async Task Heartbeat_UnknownWorker_ReturnsFalse()
    {
        var success = await _workers.UpdateHeartbeatAsync("missing");

        Assert.That(success, Is.False);
    }

    [Test]
    public async Task IncrementTasksProcessed_IncreasesCounter()
    {
        await _workers.RegisterWorkerAsync(new RegisterWorkerRequest { WorkerId = "w-1" });

        await _workers.IncrementTasksProcessedAsync("w-1");
        await _workers.IncrementTasksFailedAsync("w-1");

        var listed = await _workers.GetActiveWorkersAsync();
        Assert.That(listed.Single().TasksProcessed, Is.EqualTo(1));
        Assert.That(listed.Single().TasksFailed, Is.EqualTo(1));
    }
}
