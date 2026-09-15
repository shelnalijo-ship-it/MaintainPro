using MaintainPro.Application.WorkOrders;
using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

public sealed class GenerationConcurrencyTests
{
    [Fact]
    public async Task Competing_generators_share_one_occurrence_and_number_per_due_date_after_restart()
    {
        using var database = new TemporarySqliteDatabase();
        await using (var first = await ModuleFixture.CreateAsync(sqliteDatabasePath: database.Path))
        await using (var second = await ModuleFixture.CreateAsync(seedRoles: false, sqliteDatabasePath: database.Path))
        {
            var today = DateOnly.FromDateTime(first.Clock.GetUtcNow().UtcDateTime);
            var data = await PlanningTestData.CreateAsync(first, today.AddDays(-2));
            await data.CreatePlanAsync(first);
            second.ActAs(data.Administrator);
            second.Clock.SetUtc(first.Clock.GetUtcNow());
            using var overlap = new Barrier(2);
            WorkOrderGenerationService Generator(ModuleFixture fixture) => new(fixture.Db, fixture.Actor,
                fixture.Audit, fixture.Recurrence, fixture.Clock, fixture.Numbers,
                new OverlappingGeneration(fixture, overlap));
            var firstGenerator = Generator(first);
            var secondGenerator = Generator(second);

            var results = await Task.WhenAll(
                Task.Run(() => firstGenerator.GenerateDueAsync()),
                Task.Run(() => secondGenerator.GenerateDueAsync()));

            Assert.All(results, result => Assert.Equal(0, result.Errors));
            Assert.Equal(3, results.Sum(result => result.WorkOrdersCreated));
            first.Db.ChangeTracker.Clear();
            var orders = await first.Db.WorkOrders.AsNoTracking().ToListAsync();
            Assert.Equal(3, orders.Count);
            Assert.Equal(3, orders.Select(order => (order.MaintenancePlanId, order.PlannedDate)).Distinct().Count());
            Assert.Equal(3, orders.Select(order => order.WorkOrderNumber).Distinct().Count());
            Assert.Equal(3, (await first.Db.WorkOrderNumberSequences.SingleAsync()).LastValue);
            Assert.Equal(3, await first.Db.WorkOrderDefinitions.CountAsync());
        }

        await using var restarted = await ModuleFixture.CreateAsync(seedRoles: false, sqliteDatabasePath: database.Path);
        var administrator = await restarted.Db.Users.Include(user => user.UserRoles).ThenInclude(role => role.Role)
            .SingleAsync(user => user.UserRoles.Any(role => role.Role.Name == "ADMIN"));
        restarted.ActAs(administrator);
        Assert.Equal(0, (await restarted.Generation.GenerateDueAsync()).WorkOrdersCreated);
        Assert.Equal(3, await restarted.Db.WorkOrders.CountAsync());
    }

    [Fact]
    public async Task Concurrent_number_allocation_uses_a_persisted_annual_counter_without_lost_updates()
    {
        using var database = new TemporarySqliteDatabase();
        await using var first = await ModuleFixture.CreateAsync(sqliteDatabasePath: database.Path);
        await using var second = await ModuleFixture.CreateAsync(seedRoles: false, sqliteDatabasePath: database.Path);
        using var overlap = new Barrier(2);
        async Task<List<string>> AllocateBatch(ModuleFixture fixture)
        {
            Assert.True(overlap.SignalAndWait(TimeSpan.FromSeconds(20)), "Both independent allocation workers must overlap.");
            var allocated = new List<string>();
            for (var index = 0; index < 8; index++)
            {
                fixture.Db.ChangeTracker.Clear();
                await using var transaction = await fixture.Db.BeginTransactionAsync();
                allocated.Add(await fixture.Numbers.AllocateAsync(new DateOnly(2026, 9, 15)));
                await fixture.Db.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            return allocated;
        }

        var results = await Task.WhenAll(Task.Run(() => AllocateBatch(first)), Task.Run(() => AllocateBatch(second)));

        Assert.Equal(16, results.SelectMany(result => result).Distinct().Count());
        first.Db.ChangeTracker.Clear();
        Assert.Equal(16, (await first.Db.WorkOrderNumberSequences.SingleAsync()).LastValue);
        Assert.Equal(Enumerable.Range(1, 16).Select(value => $"WO-2026-{value:D4}").Order(),
            results.SelectMany(result => result).Order());
    }

    [Fact]
    public async Task Numbers_restart_at_year_rollover_and_expand_beyond_four_digits()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        fixture.Db.WorkOrderNumberSequences.Add(new WorkOrderNumberSequence { Year = 2026, LastValue = 9999 });
        await fixture.Db.SaveChangesAsync();
        async Task<string> Allocate(DateOnly date)
        {
            await using var transaction = await fixture.Db.BeginTransactionAsync();
            var number = await fixture.Numbers.AllocateAsync(date);
            await fixture.Db.SaveChangesAsync();
            await transaction.CommitAsync();
            return number;
        }

        Assert.Equal("WO-2026-10000", await Allocate(new DateOnly(2026, 12, 31)));
        Assert.Equal("WO-2027-0001", await Allocate(new DateOnly(2027, 1, 1)));
        Assert.Equal("WO-2027-0002", await Allocate(new DateOnly(2027, 1, 2)));
        Assert.Equal(2, await fixture.Db.WorkOrderNumberSequences.CountAsync());
    }

    [Fact]
    public async Task Number_allocation_requires_a_transaction_and_supports_multiple_allocations_before_save()
    {
        await using var fixture = await ModuleFixture.CreateAsync();
        var date = new DateOnly(2026, 1, 1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Numbers.AllocateAsync(date));
        Assert.Empty(await fixture.Db.WorkOrderNumberSequences.ToListAsync());

        await using var transaction = await fixture.Db.BeginTransactionAsync();
        Assert.Equal("WO-2026-0001", await fixture.Numbers.AllocateAsync(date));
        Assert.Equal("WO-2026-0002", await fixture.Numbers.AllocateAsync(date));
        await fixture.Db.SaveChangesAsync();
        await transaction.CommitAsync();
        Assert.Equal(2, (await fixture.Db.WorkOrderNumberSequences.SingleAsync()).LastValue);
    }

    private sealed class OverlappingGeneration(ModuleFixture fixture, Barrier overlap) : IGenerationConcurrency
    {
        private readonly SqliteGenerationConcurrency inner = new(fixture.Db);
        private int arrived;
        public void ResetTracking()
        {
            inner.ResetTracking();
            if (Interlocked.Exchange(ref arrived, 1) == 0)
                Assert.True(overlap.SignalAndWait(TimeSpan.FromSeconds(20)),
                    "Both generators must read the same pending plan before starting their transactions.");
        }
        public bool IsRetryable(Exception exception) => inner.IsRetryable(exception);
    }

    private sealed class TemporarySqliteDatabase : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            $"maintainpro-planning-test-{Guid.NewGuid():N}.sqlite");
        public void Dispose()
        {
            // These are only the randomly named files owned by this test; connections disable pooling.
            foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" }) File.Delete(Path + suffix);
        }
    }
}
