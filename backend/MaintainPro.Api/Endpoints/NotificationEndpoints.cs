using MaintainPro.Api.Security;
using MaintainPro.Application.Notifications;
using MaintainPro.Application.WorkOrders;

namespace MaintainPro.Api.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var notifications = app.MapGroup("/api/v1/notifications").WithTags("Notifications").RequireAuthorization();
        notifications.MapGet("", async ([AsParameters] NotificationQuery query, NotificationService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ListAsync(query, ct)));
        notifications.MapGet("/unread-count", async (NotificationService service, CancellationToken ct) =>
            TypedResults.Ok(await service.UnreadCountAsync(ct)));
        notifications.MapPatch("/{id:guid}/read", async (Guid id, NotificationService service, CancellationToken ct) =>
            TypedResults.Ok(await service.MarkReadAsync(id, ct)));
        notifications.MapPost("/read-all", async (NotificationService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ReadAllAsync(ct)));
        notifications.MapPost("/process-due", async (ReminderProcessingService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ProcessDueAsync(ct))).RequireAuthorization(Policies.ManageNotifications);
        var escalations = app.MapGroup("/api/v1/escalations").WithTags("Escalations").RequireAuthorization(Policies.ReadEscalations);
        escalations.MapGet("", async ([AsParameters] EscalationQuery query, EscalationQueryService service, CancellationToken ct) =>
            TypedResults.Ok(await service.ListAsync(query, ct)));
        escalations.MapGet("/{id:guid}", async (Guid id, EscalationQueryService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAsync(id, ct)));
        var settings = app.MapGroup("/api/v1/settings/escalation").WithTags("Escalation settings")
            .RequireAuthorization(Policies.ManageNotifications);
        settings.MapGet("", async (EscalationSettingsService service, CancellationToken ct) =>
            TypedResults.Ok(await service.GetAsync(ct)));
        settings.MapPut("", async (EscalationSettingsRequest request, EscalationSettingsService service, CancellationToken ct) =>
            TypedResults.Ok(await service.UpdateAsync(request, ct)));
        app.MapPost("/api/v1/work-orders/{id:guid}/assignment",
            async (Guid id, WorkOrderAssignmentRequest request, WorkOrderAssignmentService service, CancellationToken ct) =>
                TypedResults.Ok(await service.ChangeAsync(id, request, ct)))
            .RequireAuthorization(Policies.ManageNotifications).WithTags("Work orders")
            .WithSummary("Assign or reassign an active technician before execution starts");
    }
}
