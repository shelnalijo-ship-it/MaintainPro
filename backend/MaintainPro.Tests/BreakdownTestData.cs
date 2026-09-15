using MaintainPro.Application.Breakdowns;
using MaintainPro.Domain.Entities;
using MaintainPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MaintainPro.Tests;

internal sealed record BreakdownTestData(User Administrator, User Technician, User Supervisor,
    Machine Machine, BreakdownDto Breakdown)
{
    public static async Task<BreakdownTestData> CreateAsync(ModuleFixture fixture, bool stopped = true,
        bool assigned = true, BreakdownSeverity severity = BreakdownSeverity.HIGH,
        MachineStatus machineStatus = MachineStatus.Operational)
    {
        var administrator = await fixture.AsAdminAsync();
        var technician = await fixture.SeedUserAsync("TECHNICIAN");
        var supervisor = await fixture.SeedUserAsync("SUPERVISOR");
        var machine = await fixture.SeedMachineAsync(technician.Id, supervisor.Id, x => x.Status = machineStatus);
        var breakdown = await fixture.Breakdowns.ReportAsync(new(machine.Id, severity, stopped,
            "Bearing seized during operation", "Unusual noise before stopping"));
        if (assigned) breakdown = await fixture.Breakdowns.AssignAsync(breakdown.Id, new(technician.Id, Reason: "Initial corrective assignment"));
        return new(administrator, technician, supervisor, machine, breakdown);
    }

    public async Task<CorrectiveSubmissionDto> SubmitAsync(ModuleFixture fixture)
    {
        fixture.ActAs(Technician);
        await fixture.CorrectiveExecution.StartAsync(Breakdown.Id);
        await fixture.CorrectiveExecution.SaveAsync(Breakdown.Id,
            new("Bearing lacked lubrication", "Replaced bearing and restored lubrication", "Function checked"));
        await fixture.CorrectiveExecution.CompleteAsync(Breakdown.Id);
        return await fixture.CorrectiveSubmissions.SubmitAsync(Breakdown.Id);
    }

    public async Task<CorrectiveSubmissionDto> ApproveAsync(ModuleFixture fixture)
    {
        var submission = await SubmitAsync(fixture);
        fixture.ActAs(Supervisor);
        await fixture.CorrectiveReviews.ApproveAsync(Breakdown.Id, new(submission.Submission.Id, "Repair verified"));
        return submission;
    }

    public static async Task<BreakdownAttachmentDto> UploadAsync(ModuleFixture fixture, Guid id,
        BreakdownEvidenceType category = BreakdownEvidenceType.REPAIR)
    {
        var bytes = ExecutionTestData.Png();
        await using var content = new MemoryStream(bytes);
        return await fixture.BreakdownEvidence.UploadAsync(id,
            new("condition.png", "image/png", bytes.Length, category, "Condition evidence"), content);
    }
}
