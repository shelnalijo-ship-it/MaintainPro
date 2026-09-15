using System.Globalization;
using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Breakdowns;

public sealed class BreakdownNumberAllocator(IApplicationDbContext db)
{
    public async Task<string> AllocateAsync(DateTime reportedAt, CancellationToken ct = default)
    {
        if (!db.HasActiveTransaction)
            throw new InvalidOperationException("Breakdown numbering requires the reporting transaction.");
        var year = reportedAt.Year;
        var sequence = db.BreakdownNumberSequences.Local.SingleOrDefault(x => x.Year == year)
            ?? await db.BreakdownNumberSequences.SingleOrDefaultAsync(x => x.Year == year, ct);
        if (sequence is null)
        {
            sequence = new BreakdownNumberSequence { Year = year };
            db.BreakdownNumberSequences.Add(sequence);
        }
        if (sequence.LastValue == long.MaxValue) throw new AppException(409, "The annual breakdown sequence is exhausted.");
        sequence.LastValue++;
        return string.Create(CultureInfo.InvariantCulture, $"BD-{year:D4}-{sequence.LastValue:D4}");
    }
}
