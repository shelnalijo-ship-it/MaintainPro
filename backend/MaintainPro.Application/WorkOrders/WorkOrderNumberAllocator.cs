using System.Globalization;
using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.WorkOrders;

/// <summary>
/// Allocates within the caller's serializable work-order transaction. The annual primary key and
/// concurrency token arbitrate competing instances; allocation rolls back with the occurrence.
/// </summary>
public sealed class WorkOrderNumberAllocator(IApplicationDbContext db)
{
    public async Task<string> AllocateAsync(DateOnly plannedDate, CancellationToken ct = default)
    {
        if (!db.HasActiveTransaction)
            throw new InvalidOperationException("Work-order numbers must be allocated inside an occurrence transaction.");
        var sequence = db.WorkOrderNumberSequences.Local.SingleOrDefault(x => x.Year == plannedDate.Year)
            ?? await db.WorkOrderNumberSequences.SingleOrDefaultAsync(x => x.Year == plannedDate.Year, ct);
        if (sequence is null)
        {
            sequence = new WorkOrderNumberSequence { Year = plannedDate.Year };
            db.WorkOrderNumberSequences.Add(sequence);
        }

        if (sequence.LastValue == long.MaxValue)
            throw new AppException(409, "The annual work-order number sequence is exhausted.");
        sequence.LastValue++;
        return Format(sequence.Year, sequence.LastValue);
    }

    public static string Format(int year, long value)
    {
        if (year is < 1 or > 9999 || value < 1)
            throw new ArgumentOutOfRangeException(nameof(value), "A valid year and positive sequence value are required.");
        return string.Create(CultureInfo.InvariantCulture, $"WO-{year:D4}-{value:D4}");
    }
}
