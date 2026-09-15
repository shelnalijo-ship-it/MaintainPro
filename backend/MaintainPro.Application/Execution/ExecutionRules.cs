using MaintainPro.Application.Common;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;

namespace MaintainPro.Application.Execution;

public static class ExecutionRules
{
    public static void RequireTransition(WorkOrder order, WorkOrderLifecycleStatus target)
    {
        var allowed = (order.LifecycleStatus, target) switch
        {
            (WorkOrderLifecycleStatus.PLANNED or WorkOrderLifecycleStatus.ASSIGNED or WorkOrderLifecycleStatus.REJECTED,
                WorkOrderLifecycleStatus.IN_PROGRESS) => true,
            (WorkOrderLifecycleStatus.IN_PROGRESS, WorkOrderLifecycleStatus.AWAITING_APPROVAL) => true,
            (WorkOrderLifecycleStatus.AWAITING_APPROVAL,
                WorkOrderLifecycleStatus.APPROVED or WorkOrderLifecycleStatus.REJECTED) => true,
            _ => false
        };
        if (!allowed) throw new AppException(409, $"Cannot transition from {order.LifecycleStatus} to {target}.");
    }

    public static void RequireEditable(WorkOrder order)
    {
        if (order.LifecycleStatus != WorkOrderLifecycleStatus.IN_PROGRESS)
            throw new AppException(409, "Execution can only be edited while the work order is IN_PROGRESS.");
    }

    public static string? Optional(string? value, string name, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maximum) throw new AppException(400, $"{name} cannot exceed {maximum} characters.");
        return normalized;
    }

    public static void ValidateAnswer(WorkOrderChecklistItem item, WorkOrderChecklistResult result)
    {
        if (result.PassFailValue.HasValue && !Enum.IsDefined(result.PassFailValue.Value))
            throw new AppException(400, "PassFailValue must be PASS or FAIL.");
        var incompatible =
            (result.BooleanValue.HasValue && item.ResponseType != ChecklistResponseType.BOOLEAN) ||
            (result.NumericValue.HasValue && item.ResponseType != ChecklistResponseType.NUMBER) ||
            (result.TextValue is not null && item.ResponseType != ChecklistResponseType.TEXT) ||
            (result.PassFailValue.HasValue && item.ResponseType != ChecklistResponseType.PASS_FAIL) ||
            (result.ConfirmationValue.HasValue && item.ResponseType != ChecklistResponseType.CONFIRMATION);
        if (incompatible)
            throw new AppException(400, $"Checklist item '{item.Title}' accepts {item.ResponseType} responses only.");
    }

    public static NumericReadingStatus? ReadingStatus(WorkOrderChecklistItem item, decimal? value)
    {
        if (item.ResponseType != ChecklistResponseType.NUMBER || value is null) return null;
        if (item.MinimumValue is decimal minimum && value < minimum) return NumericReadingStatus.BELOW;
        if (item.MaximumValue is decimal maximum && value > maximum) return NumericReadingStatus.ABOVE;
        return NumericReadingStatus.WITHIN;
    }

    public static bool IsPhoto(WorkOrderAttachment attachment) => !attachment.IsDeleted &&
        attachment.File is { FileSize: > 0, MimeType: "image/jpeg" or "image/png" };

    public static bool IsSatisfied(WorkOrderChecklistItem item, WorkOrderChecklistResult? result,
        IReadOnlyCollection<WorkOrderAttachment> attachments) => item.ResponseType switch
    {
        ChecklistResponseType.BOOLEAN => result?.BooleanValue is not null,
        ChecklistResponseType.NUMBER => result?.NumericValue is not null,
        ChecklistResponseType.TEXT => !string.IsNullOrWhiteSpace(result?.TextValue),
        ChecklistResponseType.PASS_FAIL => result?.PassFailValue is not null,
        ChecklistResponseType.CONFIRMATION => result?.ConfirmationValue == true,
        ChecklistResponseType.PHOTO => attachments.Any(x => x.WorkOrderChecklistItemId == item.Id && IsPhoto(x)),
        _ => false
    };

    public static void ValidateForSubmission(WorkOrder order, WorkOrderExecution execution,
        IReadOnlyCollection<WorkOrderChecklistResult> results, IReadOnlyCollection<WorkOrderAttachment> attachments)
    {
        RequireTransition(order, WorkOrderLifecycleStatus.AWAITING_APPROVAL);
        if (order.StartedAt is null || order.CompletedAt is null || execution.AttemptStartedAt is not null)
            throw new AppException(400, "Complete execution before submitting the work order.");
        var errors = new List<string>();
        foreach (var item in order.Definition.Items.OrderBy(x => x.SequenceNumber))
        {
            var result = results.SingleOrDefault(x => x.WorkOrderChecklistItemId == item.Id);
            if (result is not null) ValidateAnswer(item, result);
            if (item.IsMandatory && !IsSatisfied(item, result, attachments))
                errors.Add($"Checklist item '{item.Title}' requires a {item.ResponseType} response.");
            if (item.PhotoRequired && !attachments.Any(x => x.WorkOrderChecklistItemId == item.Id && IsPhoto(x)))
                errors.Add($"Checklist item '{item.Title}' requires photo evidence.");
        }
        var requiredPhotos = Math.Max(order.Definition.MinimumPhotoCount, order.Definition.PhotoRequired ? 1 : 0);
        if (attachments.Count(IsPhoto) < requiredPhotos)
            errors.Add($"At least {requiredPhotos} maintenance photo(s) are required.");
        if (order.Definition.CommentRequired && string.IsNullOrWhiteSpace(execution.OverallComments))
            errors.Add("OverallComments are required.");
        if (attachments.Any(x => !x.IsDeleted && (x.File is null || x.File.FileSize <= 0)))
            errors.Add("All evidence uploads must be finalized before submission.");
        if (errors.Count > 0) throw new AppException(400, string.Join(" ", errors));
    }
}
