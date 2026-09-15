using MaintainPro.Application.Abstractions;
using MaintainPro.Application.Common;
using MaintainPro.Application.WorkOrders;
using MaintainPro.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Notifications;

public sealed record BreakdownNotificationProcessingSummary(int BreakdownsEvaluated, int NotificationsCreated,
    int DuplicatesSkipped, int Errors, IReadOnlyList<string> Issues);

/// <summary>Retries durable pending intents after restart, using a fresh serializable transaction per breakdown.</summary>
public sealed class BreakdownNotificationProcessor(IApplicationDbContext db, ICurrentUser currentUser,
    IAuditWriter audit, TimeProvider clock, IGenerationConcurrency concurrency)
{
    public async Task<BreakdownNotificationProcessingSummary> ProcessPendingAsync(CancellationToken ct = default)
    {
        if (currentUser.UserId.HasValue) Guard.RequireRole(currentUser, RoleNames.Manager, RoleNames.Admin);
        concurrency.ResetTracking();
        var ids = await db.BreakdownNotificationEvents.AsNoTracking().Where(x => x.ProcessedAt == null)
            .Select(x => x.BreakdownId).Distinct().OrderBy(x => x).ToListAsync(ct);
        var total = NotificationDispatchResult.Empty;
        foreach (var id in ids)
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                concurrency.ResetTracking();
                try
                {
                    await using var transaction = await db.BeginTransactionAsync(ct);
                    var breakdown = await db.Breakdowns.SingleAsync(x => x.Id == id, ct);
                    var result = await new BreakdownNotificationService(db, currentUser, audit, clock).ReplayPendingAsync(breakdown, ct);
                    await db.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                    total = total.Add(result);
                    break;
                }
                catch (Exception exception) when (attempt < 4 && concurrency.IsRetryable(exception))
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(25 * (attempt + 1)), ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception)
                {
                    total = total.Add(new(0, 0, 0, 1,
                        new[] { $"Notification processing for breakdown {id} failed and will be retried." }));
                    break;
                }
                finally { concurrency.ResetTracking(); }
            }
        }
        return new(ids.Count, total.NotificationsCreated, total.DuplicatesSkipped, total.Errors, total.Issues);
    }
}
