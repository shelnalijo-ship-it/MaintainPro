using System.Globalization;
using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.ExternalServices;

public sealed class ExternalServiceNumberAllocator(IApplicationDbContext db)
{
    public async Task<string> AllocateAsync(DateTime createdAt, CancellationToken ct = default)
    {
        if (!db.HasActiveTransaction)
            throw new InvalidOperationException("External-service numbering requires the creation transaction.");
        var year = createdAt.Year;
        var sequence = db.ExternalServiceNumberSequences.Local.SingleOrDefault(x => x.Year == year)
            ?? await db.ExternalServiceNumberSequences.SingleOrDefaultAsync(x => x.Year == year, ct);
        if (sequence is null)
        {
            sequence = new ExternalServiceNumberSequence { Year = year };
            db.ExternalServiceNumberSequences.Add(sequence);
        }
        if (sequence.LastValue == long.MaxValue)
            throw new AppException(409, "The annual external-service sequence is exhausted.");
        sequence.LastValue++;
        return string.Create(CultureInfo.InvariantCulture, $"ES-{year:D4}-{sequence.LastValue:D4}");
    }
}
