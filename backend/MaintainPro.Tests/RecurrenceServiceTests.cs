using MaintainPro.Application.Common;
using MaintainPro.Application.Planning;
using MaintainPro.Domain.Enums;

namespace MaintainPro.Tests;

public sealed class RecurrenceServiceTests
{
    [Theory]
    [InlineData(MaintenanceFrequencyType.DAILY, 1, "2026-01-31", "2026-01-31", "2026-02-01")]
    [InlineData(MaintenanceFrequencyType.WEEKLY, 1, "2026-01-31", "2026-01-31", "2026-02-07")]
    [InlineData(MaintenanceFrequencyType.DAYS, 30, "2026-01-31", "2026-01-31", "2026-03-02")]
    [InlineData(MaintenanceFrequencyType.WEEKS, 3, "2026-01-31", "2026-01-31", "2026-02-21")]
    [InlineData(MaintenanceFrequencyType.MONTHLY, 1, "2026-01-31", "2026-01-31", "2026-02-28")]
    [InlineData(MaintenanceFrequencyType.MONTHLY, 1, "2026-01-31", "2026-02-28", "2026-03-31")]
    [InlineData(MaintenanceFrequencyType.MONTHLY, 1, "2026-01-30", "2026-02-28", "2026-03-30")]
    [InlineData(MaintenanceFrequencyType.MONTHLY, 1, "2024-01-31", "2024-01-31", "2024-02-29")]
    [InlineData(MaintenanceFrequencyType.MONTHS, 2, "2026-01-31", "2026-01-31", "2026-03-31")]
    [InlineData(MaintenanceFrequencyType.QUARTERLY, 1, "2026-01-31", "2026-01-31", "2026-04-30")]
    [InlineData(MaintenanceFrequencyType.QUARTERLY, 1, "2026-01-31", "2026-04-30", "2026-07-31")]
    [InlineData(MaintenanceFrequencyType.HALF_YEARLY, 1, "2026-08-31", "2026-08-31", "2027-02-28")]
    [InlineData(MaintenanceFrequencyType.YEARLY, 1, "2024-02-29", "2024-02-29", "2025-02-28")]
    [InlineData(MaintenanceFrequencyType.YEARLY, 1, "2024-02-29", "2027-02-28", "2028-02-29")]
    public void Next_occurrence_uses_anchor_day_and_calendar_intervals(MaintenanceFrequencyType type, int value,
        string anchor, string after, string expected)
    {
        var recurrence = new RecurrenceService();

        Assert.Equal(DateOnly.Parse(expected), recurrence.Next(DateOnly.Parse(anchor), DateOnly.Parse(after), type, value));
    }

    [Fact]
    public void OnOrAfter_preserves_due_occurrences_and_finds_the_next_valid_anchor_after_missed_runs()
    {
        var recurrence = new RecurrenceService();
        var anchor = new DateOnly(2026, 1, 31);

        Assert.Equal(anchor, recurrence.OnOrAfter(anchor, anchor.AddDays(-5), MaintenanceFrequencyType.MONTHLY, 1));
        Assert.Equal(new DateOnly(2026, 2, 28), recurrence.OnOrAfter(anchor, new DateOnly(2026, 2, 28), MaintenanceFrequencyType.MONTHLY, 1));
        Assert.Equal(new DateOnly(2026, 3, 31), recurrence.OnOrAfter(anchor, new DateOnly(2026, 3, 1), MaintenanceFrequencyType.MONTHLY, 1));
        Assert.Equal(new DateOnly(2026, 3, 31), recurrence.OnOrAfter(anchor, new DateOnly(2026, 3, 31), MaintenanceFrequencyType.MONTHLY, 1));
    }

    [Theory]
    [InlineData(MaintenanceFrequencyType.DAILY, 2)]
    [InlineData(MaintenanceFrequencyType.WEEKLY, 0)]
    [InlineData(MaintenanceFrequencyType.MONTHLY, 3)]
    [InlineData(MaintenanceFrequencyType.QUARTERLY, -1)]
    [InlineData(MaintenanceFrequencyType.HALF_YEARLY, 2)]
    [InlineData(MaintenanceFrequencyType.YEARLY, 0)]
    [InlineData(MaintenanceFrequencyType.DAYS, 0)]
    [InlineData(MaintenanceFrequencyType.WEEKS, -1)]
    [InlineData(MaintenanceFrequencyType.MONTHS, 0)]
    [InlineData((MaintenanceFrequencyType)999, 1)]
    public void Invalid_frequency_values_return_validation_errors(MaintenanceFrequencyType type, int value)
    {
        var error = Assert.Throws<AppException>(() => new RecurrenceService().Validate(type, value));
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public void Calendar_overflow_is_reported_as_validation_instead_of_arithmetic_failure()
    {
        var recurrence = new RecurrenceService();
        var error = Assert.Throws<AppException>(() => recurrence.Next(DateOnly.MaxValue, DateOnly.MaxValue,
            MaintenanceFrequencyType.DAILY, 1));
        Assert.Equal(400, error.StatusCode);
        Assert.Equal(400, Assert.Throws<AppException>(() => recurrence.Next(new DateOnly(9999, 12, 1),
            new DateOnly(9999, 12, 1), MaintenanceFrequencyType.MONTHS, int.MaxValue)).StatusCode);
    }
}
