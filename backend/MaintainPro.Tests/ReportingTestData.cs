using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;

namespace MaintainPro.Tests;

internal sealed record ReportingScenario(User Manager, User Supervisor, User Technician,
    User OtherSupervisor, User OtherTechnician, Department Department, Location Location,
    Machine Machine, Machine OtherMachine, WorkOrder DueToday, WorkOrder Overdue,
    WorkOrder AwaitingApproval, WorkOrder Approved, WorkOrder Cancelled, WorkOrder Rejected,
    Breakdown OpenBreakdown, CalibrationCertificate Calibration, ExternalService ExternalService);

internal static class ReportingTestData
{
    public static readonly DateTimeOffset Now = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

    public static async Task<ReportingScenario> CreateAsync(ModuleFixture fixture)
    {
        fixture.Clock.SetUtc(Now);
        var manager = await fixture.SeedUserAsync("MANAGER");
        var supervisor = await fixture.SeedUserAsync("SUPERVISOR");
        var technician = await fixture.SeedUserAsync("TECHNICIAN");
        var otherSupervisor = await fixture.SeedUserAsync("SUPERVISOR");
        var otherTechnician = await fixture.SeedUserAsync("TECHNICIAN");
        var department = new Department { Name = "Production" };
        var otherDepartment = new Department { Name = "Utilities" };
        fixture.Db.Departments.AddRange(department, otherDepartment);
        await fixture.Db.SaveChangesAsync();
        var location = new Location { Name = "Line A", DepartmentId = department.Id };
        var otherLocation = new Location { Name = "Plant Room", DepartmentId = otherDepartment.Id };
        fixture.Db.Locations.AddRange(location, otherLocation);
        await fixture.Db.SaveChangesAsync();
        var machine = await fixture.SeedMachineAsync(technician.Id, supervisor.Id, x =>
        {
            x.DepartmentId = department.Id; x.LocationId = location.Id;
            x.Status = MachineStatus.UnderMaintenance; x.CalibrationRequired = true;
        });
        var otherMachine = await fixture.SeedMachineAsync(otherTechnician.Id, otherSupervisor.Id, x =>
        {
            x.DepartmentId = otherDepartment.Id; x.LocationId = otherLocation.Id;
            x.Status = MachineStatus.Operational; x.CalibrationRequired = true;
        });
        var type = new MaintenanceType { Name = "Preventive" };
        fixture.Db.MaintenanceTypes.Add(type);
        await fixture.Db.SaveChangesAsync();
        var plan = Plan(machine, type, technician, supervisor, manager, "Monthly inspection");
        var otherPlan = Plan(otherMachine, type, otherTechnician, otherSupervisor, manager, "Other inspection");
        fixture.Db.MaintenancePlans.AddRange(plan, otherPlan);
        await fixture.Db.SaveChangesAsync();
        var checklist = Checklist(plan, manager, "Monthly checklist");
        var otherChecklist = Checklist(otherPlan, manager, "Other checklist");
        fixture.Db.ChecklistTemplates.AddRange(checklist, otherChecklist);
        await fixture.Db.SaveChangesAsync();

        var dueToday = Order(machine, plan, checklist, type, technician, supervisor,
            "WO-2026-1001", new(2026, 9, 15), WorkOrderLifecycleStatus.ASSIGNED);
        var upcoming = Order(machine, plan, checklist, type, technician, supervisor,
            "WO-2026-1002", new(2026, 9, 18), WorkOrderLifecycleStatus.IN_PROGRESS);
        upcoming.StartedAt = Now.UtcDateTime.AddHours(-1);
        var overdue = Order(machine, plan, checklist, type, technician, supervisor,
            "WO-2026-1003", new(2026, 9, 10), WorkOrderLifecycleStatus.ASSIGNED);
        overdue.EscalationLevel = 2;
        var awaiting = Order(machine, plan, checklist, type, technician, supervisor,
            "WO-2026-1004", new(2026, 9, 14), WorkOrderLifecycleStatus.AWAITING_APPROVAL);
        awaiting.StartedAt = Now.UtcDateTime.AddDays(-2); awaiting.CompletedAt = Now.UtcDateTime.AddDays(-1);
        awaiting.SubmittedAt = Now.UtcDateTime.AddDays(-1);
        var approved = Order(machine, plan, checklist, type, technician, supervisor,
            "WO-2026-1005", new(2026, 9, 1), WorkOrderLifecycleStatus.APPROVED);
        approved.StartedAt = new DateTime(2026, 9, 2, 8, 0, 0, DateTimeKind.Utc);
        approved.CompletedAt = new DateTime(2026, 9, 2, 9, 0, 0, DateTimeKind.Utc);
        approved.SubmittedAt = new DateTime(2026, 9, 2, 9, 5, 0, DateTimeKind.Utc);
        approved.ApprovedAt = new DateTime(2026, 9, 3, 8, 0, 0, DateTimeKind.Utc);
        var cancelled = Order(machine, plan, checklist, type, technician, supervisor,
            "WO-2026-1006", new(2026, 9, 2), WorkOrderLifecycleStatus.CANCELLED);
        cancelled.CancelledAt = new DateTime(2026, 9, 2, 7, 0, 0, DateTimeKind.Utc);
        var rejected = Order(machine, plan, checklist, type, technician, supervisor,
            "WO-2026-1007", new(2026, 9, 12), WorkOrderLifecycleStatus.REJECTED);
        rejected.SubmittedAt = new DateTime(2026, 9, 12, 9, 0, 0, DateTimeKind.Utc);
        var otherOrder = Order(otherMachine, otherPlan, otherChecklist, type, otherTechnician,
            otherSupervisor, "WO-2026-2001", new(2026, 9, 15), WorkOrderLifecycleStatus.ASSIGNED);
        fixture.Db.WorkOrders.AddRange(dueToday, upcoming, overdue, awaiting, approved,
            cancelled, rejected, otherOrder);
        await fixture.Db.SaveChangesAsync();

        fixture.Db.WorkOrderExecutions.Add(new WorkOrderExecution
        {
            WorkOrderId = upcoming.Id, TechnicianId = technician.Id,
            TechnicianEmployeeId = technician.EmployeeId,
            TechnicianName = technician.FirstName + " " + technician.LastName,
            AttemptStartedAt = upcoming.StartedAt, AccumulatedDurationMinutes = 30
        });
        var approvedSubmission = Submission(approved, technician, 60, approved.SubmittedAt!.Value);
        var rejectedSubmission = Submission(rejected, technician, 45, rejected.SubmittedAt!.Value);
        var awaitingSubmission = Submission(awaiting, technician, 50, awaiting.SubmittedAt!.Value);
        fixture.Db.WorkOrderSubmissions.AddRange(approvedSubmission, rejectedSubmission, awaitingSubmission);
        await fixture.Db.SaveChangesAsync();
        fixture.Db.WorkOrderApprovals.AddRange(
            Approval(approved, approvedSubmission, supervisor, WorkOrderReviewDecision.APPROVED,
                approved.ApprovedAt!.Value),
            Approval(rejected, rejectedSubmission, supervisor, WorkOrderReviewDecision.REJECTED,
                new DateTime(2026, 9, 12, 10, 0, 0, DateTimeKind.Utc)));
        fixture.Db.WorkOrderHistoryEvents.AddRange(
            new WorkOrderHistoryEvent { WorkOrderId = approved.Id, SequenceNumber = 1,
                Action = "WorkOrder.Approved", ActorUserId = supervisor.Id,
                ActorName = supervisor.FirstName, OccurredAt = approved.ApprovedAt!.Value,
                WorkOrderSubmissionId = approvedSubmission.Id, Details = "Approved after due date." },
            new WorkOrderHistoryEvent { WorkOrderId = rejected.Id, SequenceNumber = 1,
                Action = "WorkOrder.Rejected", ActorUserId = supervisor.Id,
                ActorName = supervisor.FirstName, OccurredAt = new DateTime(2026, 9, 12, 10, 0, 0, DateTimeKind.Utc),
                WorkOrderSubmissionId = rejectedSubmission.Id, Details = "Reading requires correction." });
        var escalationNotification = new Notification
        {
            UserId = supervisor.Id, NotificationType = NotificationType.ESCALATION_SUPERVISOR,
            Title = "Overdue", Message = "Overdue work order", Priority = NotificationPriority.HIGH,
            DeduplicationKey = "reporting-escalation"
        };
        fixture.Db.Notifications.Add(escalationNotification);
        fixture.Db.WorkOrderEscalations.Add(new WorkOrderEscalation
        {
            WorkOrderId = overdue.Id, Level = 2, TriggerType = NotificationType.ESCALATION_SUPERVISOR,
            TriggeredAt = new DateTime(2026, 9, 13, 8, 0, 0, DateTimeKind.Utc),
            RecipientUserId = supervisor.Id, NotificationId = escalationNotification.Id,
            DeduplicationKey = "reporting-escalation"
        });
        await fixture.Db.SaveChangesAsync();

        var openBreakdown = Breakdown(machine, technician, supervisor, manager, "BD-2026-1001",
            BreakdownSeverity.CRITICAL, BreakdownStatus.IN_PROGRESS, true,
            new DateTime(2026, 9, 10, 8, 0, 0, DateTimeKind.Utc));
        var closedBreakdown = Breakdown(machine, technician, supervisor, manager, "BD-2026-1002",
            BreakdownSeverity.MEDIUM, BreakdownStatus.CLOSED, true,
            new DateTime(2026, 9, 2, 8, 0, 0, DateTimeKind.Utc));
        closedBreakdown.ClosedAt = new DateTime(2026, 9, 3, 9, 0, 0, DateTimeKind.Utc);
        closedBreakdown.ReturnedToServiceAt = new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc);
        var otherBreakdown = Breakdown(otherMachine, otherTechnician, otherSupervisor, manager,
            "BD-2026-2001", BreakdownSeverity.HIGH, BreakdownStatus.REPORTED, false,
            new DateTime(2026, 9, 11, 8, 0, 0, DateTimeKind.Utc));
        fixture.Db.Breakdowns.AddRange(openBreakdown, closedBreakdown, otherBreakdown);
        await fixture.Db.SaveChangesAsync();
        var corrective = new CorrectiveSubmission
        {
            BreakdownId = closedBreakdown.Id, VersionNumber = 1, TechnicianId = technician.Id,
            TechnicianEmployeeId = technician.EmployeeId,
            TechnicianName = technician.FirstName + " " + technician.LastName,
            RootCause = "Worn bearing", CorrectiveAction = "Bearing replaced",
            StartedAt = new DateTime(2026, 9, 2, 9, 0, 0, DateTimeKind.Utc),
            CompletedAt = new DateTime(2026, 9, 3, 8, 0, 0, DateTimeKind.Utc),
            SubmittedAt = new DateTime(2026, 9, 3, 8, 10, 0, DateTimeKind.Utc),
            DurationMinutes = 120, DowntimeMinutes = 1500
        };
        fixture.Db.CorrectiveSubmissions.Add(corrective);
        await fixture.Db.SaveChangesAsync();
        fixture.Db.CorrectiveApprovals.Add(new CorrectiveApproval
        {
            BreakdownId = closedBreakdown.Id, CorrectiveSubmissionId = corrective.Id,
            SupervisorId = supervisor.Id, SupervisorEmployeeId = supervisor.EmployeeId,
            SupervisorName = supervisor.FirstName + " " + supervisor.LastName,
            Decision = CorrectiveReviewDecision.APPROVED,
            DecisionAt = closedBreakdown.ClosedAt.Value
        });
        fixture.Db.BreakdownHistoryEvents.Add(new BreakdownHistoryEvent
        {
            BreakdownId = openBreakdown.Id, SequenceNumber = 1,
            Action = "Breakdown.Reported", ActorUserId = manager.Id,
            ActorName = manager.FirstName, OccurredAt = openBreakdown.ReportedAt,
            Details = "Machine stopped."
        });
        await fixture.Db.SaveChangesAsync();

        var calibration = new CalibrationCertificate
        {
            MachineId = machine.Id, CertificateNumber = "CAL-1001",
            CalibrationProvider = "Precision Labs", CalibrationDate = new(2026, 8, 1),
            ExpiryDate = new(2026, 10, 5), Result = CalibrationResult.PASS,
            CreatedByUserId = manager.Id, CreatedAt = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc)
        };
        var expiredCalibration = new CalibrationCertificate
        {
            MachineId = otherMachine.Id, CertificateNumber = "CAL-2001",
            CalibrationProvider = "Other Labs", CalibrationDate = new(2025, 9, 1),
            ExpiryDate = new(2026, 9, 1), Result = CalibrationResult.PASS,
            CreatedByUserId = manager.Id, CreatedAt = new DateTime(2025, 9, 1, 9, 0, 0, DateTimeKind.Utc)
        };
        fixture.Db.CalibrationCertificates.AddRange(calibration, expiredCalibration);
        fixture.Db.CalibrationRenewals.Add(new CalibrationRenewal
        {
            MachineId = machine.Id, PreviousCertificateId = calibration.Id,
            Status = CalibrationRenewalStatus.IN_PROGRESS,
            StartedAt = new DateTime(2026, 9, 10, 9, 0, 0, DateTimeKind.Utc),
            StartedByUserId = manager.Id
        });
        await fixture.Db.SaveChangesAsync();

        var externalService = new ExternalService
        {
            ServiceNumber = "ES-2026-1001", MachineId = machine.Id,
            MachineCode = machine.MachineCode, MachineName = machine.Name,
            ServiceCompany = "Vendor One", ServiceTechnician = "External Tech",
            ServiceDate = new(2026, 9, 5), ServiceType = ExternalServiceType.INSPECTION,
            Description = "External inspection", Findings = "Alignment issue",
            Cost = 500m, PurchaseOrderNumber = "PO-100", InvoiceNumber = "INV-100",
            FollowUpDate = new(2026, 9, 14), NextServiceDate = new(2026, 12, 1),
            CreatedByUserId = manager.Id,
            CreatedAt = new DateTime(2026, 9, 5, 11, 0, 0, DateTimeKind.Utc)
        };
        fixture.Db.ExternalServices.Add(externalService);
        var file = new FileRecord
        {
            StorageKey = "reporting/test.pdf", OriginalFilename = "service.pdf",
            MimeType = "application/pdf", FileSize = 100, UploadedByUserId = manager.Id
        };
        fixture.Db.FileRecords.Add(file);
        fixture.Db.ExternalServiceAttachments.Add(new ExternalServiceAttachment
        {
            ExternalServiceId = externalService.Id, FileId = file.Id,
            DocumentType = ExternalServiceDocumentType.SERVICE_REPORT,
            UploadedByUserId = manager.Id
        });
        fixture.Db.MachineDocuments.Add(new MachineDocument
        {
            MachineId = machine.Id, FileId = file.Id, DocumentType = MachineDocumentType.MANUAL,
            Title = "Machine manual", UploadedByUserId = manager.Id,
            UploadedAt = new DateTime(2026, 9, 6, 10, 0, 0, DateTimeKind.Utc)
        });
        fixture.Db.MachineAssignmentHistories.Add(new MachineAssignmentHistory
        {
            MachineId = machine.Id, TechnicianId = technician.Id, SupervisorId = supervisor.Id,
            AssignedByUserId = manager.Id,
            EffectiveFrom = new DateTime(2026, 8, 1, 8, 0, 0, DateTimeKind.Utc),
            Reason = "Initial assignment"
        });
        fixture.Db.AuditLogs.Add(new AuditLog
        {
            UserId = manager.Id, Action = "Machine.StatusChanged", EntityType = "Machine",
            EntityId = machine.Id.ToString(), OldValuesJson = "{\"status\":\"Operational\"}",
            NewValuesJson = "{\"status\":\"UnderMaintenance\"}",
            CreatedAt = new DateTime(2026, 9, 9, 8, 0, 0, DateTimeKind.Utc)
        });
        await fixture.Db.SaveChangesAsync();
        fixture.ActAs(manager);
        return new(manager, supervisor, technician, otherSupervisor, otherTechnician,
            department, location, machine, otherMachine, dueToday, overdue, awaiting,
            approved, cancelled, rejected, openBreakdown, calibration, externalService);
    }

    private static MaintenancePlan Plan(Machine machine, MaintenanceType type, User technician,
        User supervisor, User creator, string name) => new()
    {
        MachineId = machine.Id, PlanName = name, MaintenanceTypeId = type.Id,
        FrequencyType = MaintenanceFrequencyType.MONTHLY, StartDate = new(2026, 1, 1),
        NextDueDate = new(2026, 10, 1), DefaultTechnicianId = technician.Id,
        SupervisorId = supervisor.Id, CreatedByUserId = creator.Id
    };

    private static ChecklistTemplate Checklist(MaintenancePlan plan, User creator, string name) => new()
    {
        MaintenancePlanId = plan.Id, Version = 1, Name = name, CreatedByUserId = creator.Id
    };

    private static WorkOrder Order(Machine machine, MaintenancePlan plan, ChecklistTemplate checklist,
        MaintenanceType type, User technician, User supervisor, string number, DateOnly due,
        WorkOrderLifecycleStatus status)
    {
        var order = new WorkOrder
        {
            WorkOrderNumber = number, MachineId = machine.Id, MaintenancePlanId = plan.Id,
            AssignedTechnicianId = technician.Id, SupervisorId = supervisor.Id,
            PlannedDate = due.AddDays(-2), DueDate = due, Priority = MaintenancePriority.HIGH,
            LifecycleStatus = status, CurrentTechnicianName = technician.FirstName + " " + technician.LastName,
            CurrentTechnicianEmployeeId = technician.EmployeeId,
            CreatedAt = due.AddDays(-5).ToDateTime(new TimeOnly(8, 0), DateTimeKind.Utc)
        };
        order.Definition = new WorkOrderDefinition
        {
            WorkOrderId = order.Id, MaintenanceTypeId = type.Id, ChecklistTemplateId = checklist.Id,
            ChecklistVersion = 1, ChecklistName = checklist.Name, PlanName = plan.PlanName,
            MaintenanceTypeName = type.Name, Priority = order.Priority,
            MachineCode = machine.MachineCode, MachineName = machine.Name,
            AssignedTechnicianEmployeeId = technician.EmployeeId,
            AssignedTechnicianName = technician.FirstName + " " + technician.LastName,
            SupervisorEmployeeId = supervisor.EmployeeId,
            SupervisorName = supervisor.FirstName + " " + supervisor.LastName
        };
        return order;
    }

    private static WorkOrderSubmission Submission(WorkOrder order, User technician,
        decimal minutes, DateTime submittedAt) => new()
    {
        WorkOrderId = order.Id, VersionNumber = 1, SubmittedByUserId = technician.Id,
        TechnicianEmployeeId = technician.EmployeeId,
        TechnicianName = technician.FirstName + " " + technician.LastName,
        StartedAt = submittedAt.AddMinutes(-(double)minutes - 5),
        CompletedAt = submittedAt.AddMinutes(-5), SubmittedAt = submittedAt,
        DurationMinutes = minutes
    };

    private static WorkOrderApproval Approval(WorkOrder order, WorkOrderSubmission submission,
        User supervisor, WorkOrderReviewDecision decision, DateTime at) => new()
    {
        WorkOrderId = order.Id, WorkOrderSubmissionId = submission.Id,
        SupervisorId = supervisor.Id, SupervisorEmployeeId = supervisor.EmployeeId,
        SupervisorName = supervisor.FirstName + " " + supervisor.LastName,
        Decision = decision, Remarks = decision == WorkOrderReviewDecision.REJECTED ? "Correct reading." : null,
        DecisionAt = at
    };

    private static Breakdown Breakdown(Machine machine, User technician, User supervisor,
        User reporter, string number, BreakdownSeverity severity, BreakdownStatus status,
        bool stopped, DateTime reportedAt) => new()
    {
        BreakdownNumber = number, MachineId = machine.Id, MachineCode = machine.MachineCode,
        MachineName = machine.Name, ReportedByUserId = reporter.Id,
        ReporterEmployeeId = reporter.EmployeeId, ReporterName = reporter.FirstName + " " + reporter.LastName,
        ReportedAt = reportedAt, Severity = severity, MachineStopped = stopped,
        Description = "Unexpected machine fault", AssignedTechnicianId = technician.Id,
        TechnicianEmployeeId = technician.EmployeeId,
        TechnicianName = technician.FirstName + " " + technician.LastName,
        SupervisorId = supervisor.Id, SupervisorEmployeeId = supervisor.EmployeeId,
        SupervisorName = supervisor.FirstName + " " + supervisor.LastName,
        Status = status, PreviousMachineStatus = MachineStatus.Operational,
        RestoreMachineStatus = MachineStatus.Operational,
        MachineStatusVersionAtStop = stopped ? Guid.NewGuid() : null,
        StartedAt = status == BreakdownStatus.REPORTED ? null : reportedAt.AddMinutes(30)
    };
}
