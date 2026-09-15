using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintainPro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BreakdownAndCorrectiveMaintenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StatusVersion",
                table: "Machines",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "BreakdownNumberSequences",
                columns: table => new
                {
                    Year = table.Column<int>(type: "integer", nullable: false),
                    LastValue = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BreakdownNumberSequences", x => x.Year);
                    table.CheckConstraint("CK_BreakdownNumberSequences_Value", "\"LastValue\" >= 0");
                    table.CheckConstraint("CK_BreakdownNumberSequences_Year", "\"Year\" BETWEEN 1 AND 9999");
                });

            migrationBuilder.CreateTable(
                name: "Breakdowns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BreakdownNumber = table.Column<string>(type: "text", nullable: false),
                    MachineId = table.Column<Guid>(type: "uuid", nullable: false),
                    MachineCode = table.Column<string>(type: "text", nullable: false),
                    MachineName = table.Column<string>(type: "text", nullable: false),
                    ReportedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterEmployeeId = table.Column<string>(type: "text", nullable: false),
                    ReporterName = table.Column<string>(type: "text", nullable: false),
                    ReportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Severity = table.Column<string>(type: "text", nullable: false),
                    MachineStopped = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    InitialObservation = table.Column<string>(type: "text", nullable: true),
                    AssignedTechnicianId = table.Column<Guid>(type: "uuid", nullable: true),
                    TechnicianEmployeeId = table.Column<string>(type: "text", nullable: true),
                    TechnicianName = table.Column<string>(type: "text", nullable: true),
                    SupervisorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupervisorEmployeeId = table.Column<string>(type: "text", nullable: false),
                    SupervisorName = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    PreviousMachineStatus = table.Column<string>(type: "text", nullable: true),
                    RestoreMachineStatus = table.Column<string>(type: "text", nullable: true),
                    MachineStatusVersionAtStop = table.Column<Guid>(type: "uuid", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReturnedToServiceAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentVersion = table.Column<int>(type: "integer", nullable: false),
                    SubmissionVersion = table.Column<int>(type: "integer", nullable: false),
                    HistoryVersion = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Breakdowns", x => x.Id);
                    table.CheckConstraint("CK_Breakdowns_ReturnedToService", "\"ReturnedToServiceAt\" IS NULL OR (\"MachineStopped\" AND \"ClosedAt\" IS NOT NULL AND \"Status\" = 'CLOSED' AND \"ReturnedToServiceAt\" >= \"ReportedAt\")");
                    table.CheckConstraint("CK_Breakdowns_Versions", "\"AssignmentVersion\" >= 0 AND \"SubmissionVersion\" >= 0 AND \"HistoryVersion\" >= 0");
                    table.ForeignKey(
                        name: "FK_Breakdowns_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Breakdowns_Users_AssignedTechnicianId",
                        column: x => x.AssignedTechnicianId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Breakdowns_Users_ReportedByUserId",
                        column: x => x.ReportedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Breakdowns_Users_SupervisorId",
                        column: x => x.SupervisorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BreakdownAssignmentHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BreakdownId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceNumber = table.Column<int>(type: "integer", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianEmployeeId = table.Column<string>(type: "text", nullable: false),
                    TechnicianName = table.Column<string>(type: "text", nullable: false),
                    SupervisorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupervisorEmployeeId = table.Column<string>(type: "text", nullable: false),
                    SupervisorName = table.Column<string>(type: "text", nullable: false),
                    AssignedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BreakdownAssignmentHistories", x => x.Id);
                    table.CheckConstraint("CK_BreakdownAssignmentHistories_Sequence", "\"SequenceNumber\" > 0");
                    table.ForeignKey(
                        name: "FK_BreakdownAssignmentHistories_Breakdowns_BreakdownId",
                        column: x => x.BreakdownId,
                        principalTable: "Breakdowns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BreakdownAssignmentHistories_Users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BreakdownAssignmentHistories_Users_SupervisorId",
                        column: x => x.SupervisorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BreakdownAssignmentHistories_Users_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BreakdownAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BreakdownId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceType = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BreakdownAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BreakdownAttachments_Breakdowns_BreakdownId",
                        column: x => x.BreakdownId,
                        principalTable: "Breakdowns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BreakdownAttachments_FileRecords_FileId",
                        column: x => x.FileId,
                        principalTable: "FileRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BreakdownAttachments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BreakdownNotificationEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BreakdownId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationType = table.Column<string>(type: "text", nullable: false),
                    EventReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignmentVersion = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    DeduplicationKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BreakdownNotificationEvents", x => x.Id);
                    table.CheckConstraint("CK_BreakdownNotificationEvents_AssignmentVersion", "\"AssignmentVersion\" >= 0");
                    table.ForeignKey(
                        name: "FK_BreakdownNotificationEvents_Breakdowns_BreakdownId",
                        column: x => x.BreakdownId,
                        principalTable: "Breakdowns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CorrectiveActionDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BreakdownId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianEmployeeId = table.Column<string>(type: "text", nullable: false),
                    TechnicianName = table.Column<string>(type: "text", nullable: false),
                    RootCause = table.Column<string>(type: "text", nullable: true),
                    CorrectiveAction = table.Column<string>(type: "text", nullable: true),
                    Comments = table.Column<string>(type: "text", nullable: true),
                    AttemptStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AccumulatedDurationMinutes = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrectiveActionDrafts", x => x.Id);
                    table.CheckConstraint("CK_CorrectiveActionDrafts_Duration", "\"AccumulatedDurationMinutes\" >= 0");
                    table.ForeignKey(
                        name: "FK_CorrectiveActionDrafts_Breakdowns_BreakdownId",
                        column: x => x.BreakdownId,
                        principalTable: "Breakdowns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrectiveActionDrafts_Users_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CorrectivePartUsages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BreakdownId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartName = table.Column<string>(type: "text", nullable: false),
                    PartNumber = table.Column<string>(type: "text", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrectivePartUsages", x => x.Id);
                    table.CheckConstraint("CK_CorrectivePartUsages_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_CorrectivePartUsages_Breakdowns_BreakdownId",
                        column: x => x.BreakdownId,
                        principalTable: "Breakdowns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrectivePartUsages_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CorrectiveSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BreakdownId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianEmployeeId = table.Column<string>(type: "text", nullable: false),
                    TechnicianName = table.Column<string>(type: "text", nullable: false),
                    RootCause = table.Column<string>(type: "text", nullable: false),
                    CorrectiveAction = table.Column<string>(type: "text", nullable: false),
                    Comments = table.Column<string>(type: "text", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMinutes = table.Column<decimal>(type: "numeric", nullable: false),
                    DowntimeMinutes = table.Column<decimal>(type: "numeric", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrectiveSubmissions", x => x.Id);
                    table.CheckConstraint("CK_CorrectiveSubmissions_Durations", "\"DurationMinutes\" >= 0 AND \"DowntimeMinutes\" >= 0");
                    table.CheckConstraint("CK_CorrectiveSubmissions_Times", "\"CompletedAt\" >= \"StartedAt\" AND \"SubmittedAt\" >= \"CompletedAt\"");
                    table.CheckConstraint("CK_CorrectiveSubmissions_Version", "\"VersionNumber\" > 0");
                    table.ForeignKey(
                        name: "FK_CorrectiveSubmissions_Breakdowns_BreakdownId",
                        column: x => x.BreakdownId,
                        principalTable: "Breakdowns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrectiveSubmissions_Users_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BreakdownHistoryEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BreakdownId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceNumber = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorName = table.Column<string>(type: "text", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CorrectiveSubmissionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubmissionVersion = table.Column<int>(type: "integer", nullable: true),
                    Details = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BreakdownHistoryEvents", x => x.Id);
                    table.CheckConstraint("CK_BreakdownHistoryEvents_Sequence", "\"SequenceNumber\" > 0");
                    table.ForeignKey(
                        name: "FK_BreakdownHistoryEvents_Breakdowns_BreakdownId",
                        column: x => x.BreakdownId,
                        principalTable: "Breakdowns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BreakdownHistoryEvents_CorrectiveSubmissions_CorrectiveSubm~",
                        column: x => x.CorrectiveSubmissionId,
                        principalTable: "CorrectiveSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BreakdownHistoryEvents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CorrectiveApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BreakdownId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrectiveSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupervisorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupervisorEmployeeId = table.Column<string>(type: "text", nullable: false),
                    SupervisorName = table.Column<string>(type: "text", nullable: false),
                    Decision = table.Column<string>(type: "text", nullable: false),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    DecisionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrectiveApprovals", x => x.Id);
                    table.CheckConstraint("CK_CorrectiveApprovals_RejectionRemarks", "\"Decision\" <> 'REJECTED' OR (\"Remarks\" IS NOT NULL AND length(trim(\"Remarks\")) > 0)");
                    table.ForeignKey(
                        name: "FK_CorrectiveApprovals_Breakdowns_BreakdownId",
                        column: x => x.BreakdownId,
                        principalTable: "Breakdowns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrectiveApprovals_CorrectiveSubmissions_CorrectiveSubmiss~",
                        column: x => x.CorrectiveSubmissionId,
                        principalTable: "CorrectiveSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrectiveApprovals_Users_SupervisorId",
                        column: x => x.SupervisorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CorrectiveSubmissionAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrectiveSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceType = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrectiveSubmissionAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrectiveSubmissionAttachments_CorrectiveSubmissions_Corre~",
                        column: x => x.CorrectiveSubmissionId,
                        principalTable: "CorrectiveSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrectiveSubmissionAttachments_FileRecords_FileId",
                        column: x => x.FileId,
                        principalTable: "FileRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrectiveSubmissionAttachments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CorrectiveSubmissionPartUsages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrectiveSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartName = table.Column<string>(type: "text", nullable: false),
                    PartNumber = table.Column<string>(type: "text", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrectiveSubmissionPartUsages", x => x.Id);
                    table.CheckConstraint("CK_CorrectiveSubmissionPartUsages_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_CorrectiveSubmissionPartUsages_CorrectiveSubmissions_Correc~",
                        column: x => x.CorrectiveSubmissionId,
                        principalTable: "CorrectiveSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrectiveSubmissionPartUsages_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BreakdownAssignmentHistories_AssignedByUserId",
                table: "BreakdownAssignmentHistories",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BreakdownAssignmentHistories_BreakdownId_SequenceNumber",
                table: "BreakdownAssignmentHistories",
                columns: new[] { "BreakdownId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BreakdownAssignmentHistories_SupervisorId",
                table: "BreakdownAssignmentHistories",
                column: "SupervisorId");

            migrationBuilder.CreateIndex(
                name: "IX_BreakdownAssignmentHistories_TechnicianId",
                table: "BreakdownAssignmentHistories",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_BreakdownAttachments_BreakdownId_FileId",
                table: "BreakdownAttachments",
                columns: new[] { "BreakdownId", "FileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BreakdownAttachments_FileId",
                table: "BreakdownAttachments",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_BreakdownAttachments_UploadedByUserId",
                table: "BreakdownAttachments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BreakdownHistoryEvents_ActorUserId",
                table: "BreakdownHistoryEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BreakdownHistoryEvents_BreakdownId_SequenceNumber",
                table: "BreakdownHistoryEvents",
                columns: new[] { "BreakdownId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BreakdownHistoryEvents_CorrectiveSubmissionId",
                table: "BreakdownHistoryEvents",
                column: "CorrectiveSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_BreakdownNotificationEvents_BreakdownId",
                table: "BreakdownNotificationEvents",
                column: "BreakdownId");

            migrationBuilder.CreateIndex(
                name: "IX_BreakdownNotificationEvents_DeduplicationKey",
                table: "BreakdownNotificationEvents",
                column: "DeduplicationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BreakdownNotificationEvents_ProcessedAt_BreakdownId",
                table: "BreakdownNotificationEvents",
                columns: new[] { "ProcessedAt", "BreakdownId" });

            migrationBuilder.CreateIndex(
                name: "IX_Breakdowns_AssignedTechnicianId_Status",
                table: "Breakdowns",
                columns: new[] { "AssignedTechnicianId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Breakdowns_BreakdownNumber",
                table: "Breakdowns",
                column: "BreakdownNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Breakdowns_MachineId_MachineStopped_ReturnedToServiceAt",
                table: "Breakdowns",
                columns: new[] { "MachineId", "MachineStopped", "ReturnedToServiceAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Breakdowns_ReportedByUserId",
                table: "Breakdowns",
                column: "ReportedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Breakdowns_Status_ReportedAt",
                table: "Breakdowns",
                columns: new[] { "Status", "ReportedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Breakdowns_SupervisorId_Status",
                table: "Breakdowns",
                columns: new[] { "SupervisorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActionDrafts_BreakdownId",
                table: "CorrectiveActionDrafts",
                column: "BreakdownId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveActionDrafts_TechnicianId",
                table: "CorrectiveActionDrafts",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveApprovals_BreakdownId",
                table: "CorrectiveApprovals",
                column: "BreakdownId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveApprovals_CorrectiveSubmissionId",
                table: "CorrectiveApprovals",
                column: "CorrectiveSubmissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveApprovals_SupervisorId",
                table: "CorrectiveApprovals",
                column: "SupervisorId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectivePartUsages_BreakdownId",
                table: "CorrectivePartUsages",
                column: "BreakdownId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectivePartUsages_CreatedByUserId",
                table: "CorrectivePartUsages",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveSubmissionAttachments_CorrectiveSubmissionId_File~",
                table: "CorrectiveSubmissionAttachments",
                columns: new[] { "CorrectiveSubmissionId", "FileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveSubmissionAttachments_FileId",
                table: "CorrectiveSubmissionAttachments",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveSubmissionAttachments_UploadedByUserId",
                table: "CorrectiveSubmissionAttachments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveSubmissionPartUsages_CorrectiveSubmissionId",
                table: "CorrectiveSubmissionPartUsages",
                column: "CorrectiveSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveSubmissionPartUsages_CreatedByUserId",
                table: "CorrectiveSubmissionPartUsages",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveSubmissions_BreakdownId_VersionNumber",
                table: "CorrectiveSubmissions",
                columns: new[] { "BreakdownId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorrectiveSubmissions_TechnicianId",
                table: "CorrectiveSubmissions",
                column: "TechnicianId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BreakdownAssignmentHistories");

            migrationBuilder.DropTable(
                name: "BreakdownAttachments");

            migrationBuilder.DropTable(
                name: "BreakdownHistoryEvents");

            migrationBuilder.DropTable(
                name: "BreakdownNotificationEvents");

            migrationBuilder.DropTable(
                name: "BreakdownNumberSequences");

            migrationBuilder.DropTable(
                name: "CorrectiveActionDrafts");

            migrationBuilder.DropTable(
                name: "CorrectiveApprovals");

            migrationBuilder.DropTable(
                name: "CorrectivePartUsages");

            migrationBuilder.DropTable(
                name: "CorrectiveSubmissionAttachments");

            migrationBuilder.DropTable(
                name: "CorrectiveSubmissionPartUsages");

            migrationBuilder.DropTable(
                name: "CorrectiveSubmissions");

            migrationBuilder.DropTable(
                name: "Breakdowns");

            migrationBuilder.DropColumn(
                name: "StatusVersion",
                table: "Machines");
        }
    }
}
