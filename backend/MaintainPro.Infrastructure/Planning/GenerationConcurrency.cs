using MaintainPro.Application.WorkOrders;
using MaintainPro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MaintainPro.Infrastructure.Planning;

public sealed class GenerationConcurrency(ApplicationDbContext db) : IGenerationConcurrency
{
    public void ResetTracking() => db.ChangeTracker.Clear();

    public bool IsRetryable(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException) return true;
            if (current is PostgresException { SqlState: "40001" or "40P01" or "23505" }) return true;
        }
        return false;
    }
}
