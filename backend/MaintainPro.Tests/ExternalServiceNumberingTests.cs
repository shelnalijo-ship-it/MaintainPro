using MaintainPro.Application.ExternalServices;
using MaintainPro.Application.WorkOrders;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class ExternalServiceNumberingTests
{
    [Fact]
    public async Task Annual_numbers_restart_and_expand_beyond_four_digits()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var data = await ExternalServiceTestData.CreateAsync(fixture);
        fixture.Db.ExternalServiceNumberSequences.Add(new ExternalServiceNumberSequence { Year = 2026, LastValue = 9999 });
        await fixture.Db.SaveChangesAsync();
        fixture.Clock.SetUtc(new DateTimeOffset(2026, 12, 31, 12, 0, 0, TimeSpan.Zero));
        var first = await fixture.ExternalServices.CreateAsync(Request(data.Machine.Id, new DateOnly(2026, 12, 31), "First"));
        fixture.Clock.SetUtc(new DateTimeOffset(2027, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var second = await fixture.ExternalServices.CreateAsync(Request(data.Machine.Id, new DateOnly(2027, 1, 1), "Second"));

        Assert.Equal("ES-2026-10000", first.ServiceNumber);
        Assert.Equal("ES-2027-0001", second.ServiceNumber);
        Assert.Equal(2, await fixture.Db.ExternalServiceNumberSequences.CountAsync());
    }

    [Fact]
    public async Task Number_allocation_requires_a_transaction()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ExternalNumbers.AllocateAsync(DateTime.UtcNow));
        Assert.Empty(await fixture.Db.ExternalServiceNumberSequences.ToListAsync());
    }

    [Fact]
    public async Task Concurrent_creators_share_one_persistent_sequence_without_duplicates_after_restart()
    {
        using var database = new TemporaryExternalServiceDatabase();
        DateTimeOffset now;
        Guid machineId;
        await using (var first = await ModuleFixture.CreateAsync(sqliteDatabasePath: database.Path))
        await using (var second = await ModuleFixture.CreateAsync(seedRoles: false, sqliteDatabasePath: database.Path))
        {
            var data = await ExternalServiceTestData.CreateAsync(first);
            now = first.Clock.GetUtcNow();
            machineId = data.Machine.Id;
            second.ActAs(data.Administrator);
            second.Clock.SetUtc(now);
            using var overlap = new Barrier(2);
            ExternalServiceService Service(ModuleFixture fixture) => new(fixture.Db, fixture.Actor,
                fixture.Audit, fixture.Clock, new ExternalServiceNumberAllocator(fixture.Db),
                new OverlappingExternalServices(fixture, overlap));
            var date = DateOnly.FromDateTime(now.UtcDateTime);

            var results = await Task.WhenAll(
                Task.Run(() => Service(first).CreateAsync(Request(machineId, date, "One"))),
                Task.Run(() => Service(second).CreateAsync(Request(machineId, date, "Two"))));

            Assert.Equal(2, results.Select(x => x.ServiceNumber).Distinct().Count());
            Assert.Equal(new[] { $"ES-{date.Year:D4}-0001", $"ES-{date.Year:D4}-0002" },
                results.Select(x => x.ServiceNumber).Order());
            first.Db.ChangeTracker.Clear();
            Assert.Equal(2, await first.Db.ExternalServices.CountAsync());
            Assert.Equal(2, (await first.Db.ExternalServiceNumberSequences.SingleAsync()).LastValue);
        }

        await using var restarted = await ModuleFixture.CreateAsync(seedRoles: false, sqliteDatabasePath: database.Path);
        Assert.Equal(2, await restarted.Db.ExternalServices.Select(x => x.ServiceNumber).Distinct().CountAsync());
        Assert.Equal(2, (await restarted.Db.ExternalServiceNumberSequences.SingleAsync()).LastValue);
    }

    private static ExternalServiceWriteRequest Request(Guid machineId, DateOnly date, string description) =>
        new(machineId, "Provider", "Technician", date, ExternalServiceType.OTHER, description);

    private sealed class OverlappingExternalServices(ModuleFixture fixture, Barrier overlap) : IGenerationConcurrency
    {
        private readonly SqliteGenerationConcurrency inner = new(fixture.Db);
        private int entered;
        public void ResetTracking()
        {
            inner.ResetTracking();
            if (Interlocked.Exchange(ref entered, 1) == 0)
                Assert.True(overlap.SignalAndWait(TimeSpan.FromSeconds(20)),
                    "Both creators must capture the same sequence state before either transaction completes.");
        }
        public bool IsRetryable(Exception exception) => inner.IsRetryable(exception);
    }

    private sealed class TemporaryExternalServiceDatabase : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            $"maintainpro-external-service-test-{Guid.NewGuid():N}.sqlite");
        public void Dispose()
        {
            foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" }) File.Delete(Path + suffix);
        }
    }
}
