using System.Text.Json;
using MaintainPro.Application.Abstractions;
using MaintainPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Application.Breakdowns;

public static class BreakdownHistory
{
    public static void Record(IApplicationDbContext db, ICurrentUser user, Breakdown breakdown,
        string action, DateTime now, Guid? submissionId = null, int? submissionVersion = null, object? details = null)
    {
        if (!db.HasActiveTransaction)
            throw new InvalidOperationException("Breakdown history requires the workflow transaction.");
        var actor = user.UserId.HasValue
            ? db.Users.Local.FirstOrDefault(x => x.Id == user.UserId) ??
              db.Users.AsNoTracking().SingleOrDefault(x => x.Id == user.UserId)
            : null;
        breakdown.HistoryVersion = checked(breakdown.HistoryVersion + 1);
        db.BreakdownHistoryEvents.Add(new BreakdownHistoryEvent
        {
            BreakdownId = breakdown.Id, SequenceNumber = breakdown.HistoryVersion,
            Action = action, ActorUserId = user.UserId,
            ActorName = actor is null ? null : actor.FirstName + " " + actor.LastName,
            OccurredAt = now, CorrectiveSubmissionId = submissionId, SubmissionVersion = submissionVersion,
            Details = details is null ? null : JsonSerializer.Serialize(details)
        });
    }
}
