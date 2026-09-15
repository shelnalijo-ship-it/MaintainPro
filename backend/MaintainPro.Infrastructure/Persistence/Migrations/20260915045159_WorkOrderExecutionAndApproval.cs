using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintainPro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkOrderExecutionAndApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HistoryVersion",
                table: "WorkOrders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SubmissionVersion",
                table: "WorkOrders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "FileRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "text", nullable: false),
                    OriginalFilename = table.Column<string>(type: "text", nullable: false),
                    MimeType = table.Column<string>(type: "text", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileRecords", x => x.Id);
                    table.CheckConstraint("CK_FileRecords_Size", "\"FileSize\" > 0");
                    table.ForeignKey(
                        name: "FK_FileRecords_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SparePartUsages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartName = table.Column<string>(type: "text", nullable: false),
                    PartNumber = table.Column<string>(type: "text", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SparePartUsages", x => x.Id);
                    table.CheckConstraint("CK_SparePartUsages_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_SparePartUsages_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SparePartUsages_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderChecklistResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderChecklistItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    BooleanValue = table.Column<bool>(type: "boolean", nullable: true),
                    NumericValue = table.Column<decimal>(type: "numeric", nullable: true),
                    TextValue = table.Column<string>(type: "text", nullable: true),
                    PassFailValue = table.Column<string>(type: "text", nullable: true),
                    ConfirmationValue = table.Column<bool>(type: "boolean", nullable: true),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderChecklistResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderChecklistResults_Users_CompletedByUserId",
                        column: x => x.CompletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderChecklistResults_WorkOrderChecklistItems_WorkOrder~",
                        column: x => x.WorkOrderChecklistItemId,
                        principalTable: "WorkOrderChecklistItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderChecklistResults_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderDefects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<string>(type: "text", nullable: true),
                    RequiresFollowUp = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderDefects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderDefects_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderDefects_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianEmployeeId = table.Column<string>(type: "text", nullable: false),
                    TechnicianName = table.Column<string>(type: "text", nullable: false),
                    OverallComments = table.Column<string>(type: "text", nullable: true),
                    Observations = table.Column<string>(type: "text", nullable: true),
                    AttemptStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AccumulatedDurationMinutes = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderExecutions", x => x.Id);
                    table.CheckConstraint("CK_WorkOrderExecutions_Duration", "\"AccumulatedDurationMinutes\" >= 0");
                    table.ForeignKey(
                        name: "FK_WorkOrderExecutions_Users_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderExecutions_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicianEmployeeId = table.Column<string>(type: "text", nullable: false),
                    TechnicianName = table.Column<string>(type: "text", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OverallComments = table.Column<string>(type: "text", nullable: true),
                    Observations = table.Column<string>(type: "text", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMinutes = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderSubmissions", x => x.Id);
                    table.CheckConstraint("CK_WorkOrderSubmissions_VersionAndDuration", "\"VersionNumber\" > 0 AND \"DurationMinutes\" >= 0 AND \"CompletedAt\" >= \"StartedAt\" AND \"SubmittedAt\" >= \"CompletedAt\"");
                    table.ForeignKey(
                        name: "FK_WorkOrderSubmissions_Users_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderSubmissions_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderChecklistItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceType = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderAttachments_FileRecords_FileId",
                        column: x => x.FileId,
                        principalTable: "FileRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderAttachments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderAttachments_WorkOrderChecklistItems_WorkOrderCheck~",
                        column: x => x.WorkOrderChecklistItemId,
                        principalTable: "WorkOrderChecklistItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderAttachments_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupervisorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupervisorEmployeeId = table.Column<string>(type: "text", nullable: false),
                    SupervisorName = table.Column<string>(type: "text", nullable: false),
                    Decision = table.Column<string>(type: "text", nullable: false),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    DecisionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderApprovals", x => x.Id);
                    table.CheckConstraint("CK_WorkOrderApprovals_RejectionReason", "\"Decision\" <> 'REJECTED' OR (\"Remarks\" IS NOT NULL AND length(trim(\"Remarks\")) > 0)");
                    table.ForeignKey(
                        name: "FK_WorkOrderApprovals_Users_SupervisorId",
                        column: x => x.SupervisorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderApprovals_WorkOrderSubmissions_WorkOrderSubmission~",
                        column: x => x.WorkOrderSubmissionId,
                        principalTable: "WorkOrderSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderApprovals_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderHistoryEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceNumber = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorName = table.Column<string>(type: "text", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WorkOrderSubmissionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Details = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderHistoryEvents", x => x.Id);
                    table.CheckConstraint("CK_WorkOrderHistoryEvents_Sequence", "\"SequenceNumber\" > 0");
                    table.ForeignKey(
                        name: "FK_WorkOrderHistoryEvents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderHistoryEvents_WorkOrderSubmissions_WorkOrderSubmis~",
                        column: x => x.WorkOrderSubmissionId,
                        principalTable: "WorkOrderSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderHistoryEvents_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderSubmissionAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderChecklistItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceType = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderSubmissionAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderSubmissionAttachments_FileRecords_FileId",
                        column: x => x.FileId,
                        principalTable: "FileRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderSubmissionAttachments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderSubmissionAttachments_WorkOrderChecklistItems_Work~",
                        column: x => x.WorkOrderChecklistItemId,
                        principalTable: "WorkOrderChecklistItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderSubmissionAttachments_WorkOrderSubmissions_WorkOrd~",
                        column: x => x.WorkOrderSubmissionId,
                        principalTable: "WorkOrderSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderSubmissionChecklistResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderChecklistItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    BooleanValue = table.Column<bool>(type: "boolean", nullable: true),
                    NumericValue = table.Column<decimal>(type: "numeric", nullable: true),
                    TextValue = table.Column<string>(type: "text", nullable: true),
                    PassFailValue = table.Column<string>(type: "text", nullable: true),
                    ConfirmationValue = table.Column<bool>(type: "boolean", nullable: true),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderSubmissionChecklistResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderSubmissionChecklistResults_Users_CompletedByUserId",
                        column: x => x.CompletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderSubmissionChecklistResults_WorkOrderChecklistItems~",
                        column: x => x.WorkOrderChecklistItemId,
                        principalTable: "WorkOrderChecklistItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderSubmissionChecklistResults_WorkOrderSubmissions_Wo~",
                        column: x => x.WorkOrderSubmissionId,
                        principalTable: "WorkOrderSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderSubmissionDefects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<string>(type: "text", nullable: true),
                    RequiresFollowUp = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderSubmissionDefects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderSubmissionDefects_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderSubmissionDefects_WorkOrderSubmissions_WorkOrderSu~",
                        column: x => x.WorkOrderSubmissionId,
                        principalTable: "WorkOrderSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderSubmissionPartUsages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderSubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartName = table.Column<string>(type: "text", nullable: false),
                    PartNumber = table.Column<string>(type: "text", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    Remarks = table.Column<string>(type: "text", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderSubmissionPartUsages", x => x.Id);
                    table.CheckConstraint("CK_WorkOrderSubmissionPartUsages_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_WorkOrderSubmissionPartUsages_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderSubmissionPartUsages_WorkOrderSubmissions_WorkOrde~",
                        column: x => x.WorkOrderSubmissionId,
                        principalTable: "WorkOrderSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_SupervisorId_LifecycleStatus_SubmittedAt",
                table: "WorkOrders",
                columns: new[] { "SupervisorId", "LifecycleStatus", "SubmittedAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_WorkOrders_ExecutionCounters",
                table: "WorkOrders",
                sql: "\"SubmissionVersion\" >= 0 AND \"HistoryVersion\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_FileRecords_StorageKey",
                table: "FileRecords",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileRecords_UploadedByUserId",
                table: "FileRecords",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SparePartUsages_CreatedByUserId",
                table: "SparePartUsages",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SparePartUsages_WorkOrderId",
                table: "SparePartUsages",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderApprovals_SupervisorId",
                table: "WorkOrderApprovals",
                column: "SupervisorId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderApprovals_WorkOrderId",
                table: "WorkOrderApprovals",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderApprovals_WorkOrderSubmissionId",
                table: "WorkOrderApprovals",
                column: "WorkOrderSubmissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderAttachments_FileId",
                table: "WorkOrderAttachments",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderAttachments_UploadedByUserId",
                table: "WorkOrderAttachments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderAttachments_WorkOrderChecklistItemId",
                table: "WorkOrderAttachments",
                column: "WorkOrderChecklistItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderAttachments_WorkOrderId_IsDeleted",
                table: "WorkOrderAttachments",
                columns: new[] { "WorkOrderId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderChecklistResults_CompletedByUserId",
                table: "WorkOrderChecklistResults",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderChecklistResults_WorkOrderChecklistItemId",
                table: "WorkOrderChecklistResults",
                column: "WorkOrderChecklistItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderChecklistResults_WorkOrderId_WorkOrderChecklistIte~",
                table: "WorkOrderChecklistResults",
                columns: new[] { "WorkOrderId", "WorkOrderChecklistItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderDefects_CreatedByUserId",
                table: "WorkOrderDefects",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderDefects_WorkOrderId",
                table: "WorkOrderDefects",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderExecutions_TechnicianId",
                table: "WorkOrderExecutions",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderExecutions_WorkOrderId",
                table: "WorkOrderExecutions",
                column: "WorkOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderHistoryEvents_ActorUserId",
                table: "WorkOrderHistoryEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderHistoryEvents_WorkOrderId_SequenceNumber",
                table: "WorkOrderHistoryEvents",
                columns: new[] { "WorkOrderId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderHistoryEvents_WorkOrderSubmissionId",
                table: "WorkOrderHistoryEvents",
                column: "WorkOrderSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderSubmissionAttachments_FileId",
                table: "WorkOrderSubmissionAttachments",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderSubmissionAttachments_UploadedByUserId",
                table: "WorkOrderSubmissionAttachments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderSubmissionAttachments_WorkOrderChecklistItemId",
                table: "WorkOrderSubmissionAttachments",
                column: "WorkOrderChecklistItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderSubmissionAttachments_WorkOrderSubmissionId_FileId",
                table: "WorkOrderSubmissionAttachments",
                columns: new[] { "WorkOrderSubmissionId", "FileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderSubmissionChecklistResults_CompletedByUserId",
                table: "WorkOrderSubmissionChecklistResults",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderSubmissionChecklistResults_WorkOrderChecklistItemId",
                table: "WorkOrderSubmissionChecklistResults",
                column: "WorkOrderChecklistItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderSubmissionChecklistResults_WorkOrderSubmissionId_W~",
                table: "WorkOrderSubmissionChecklistResults",
                columns: new[] { "WorkOrderSubmissionId", "WorkOrderChecklistItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderSubmissionDefects_CreatedByUserId",
                table: "WorkOrderSubmissionDefects",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderSubmissionDefects_WorkOrderSubmissionId",
                table: "WorkOrderSubmissionDefects",
                column: "WorkOrderSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderSubmissionPartUsages_CreatedByUserId",
                table: "WorkOrderSubmissionPartUsages",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderSubmissionPartUsages_WorkOrderSubmissionId",
                table: "WorkOrderSubmissionPartUsages",
                column: "WorkOrderSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderSubmissions_SubmittedByUserId",
                table: "WorkOrderSubmissions",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderSubmissions_WorkOrderId_VersionNumber",
                table: "WorkOrderSubmissions",
                columns: new[] { "WorkOrderId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SparePartUsages");

            migrationBuilder.DropTable(
                name: "WorkOrderApprovals");

            migrationBuilder.DropTable(
                name: "WorkOrderAttachments");

            migrationBuilder.DropTable(
                name: "WorkOrderChecklistResults");

            migrationBuilder.DropTable(
                name: "WorkOrderDefects");

            migrationBuilder.DropTable(
                name: "WorkOrderExecutions");

            migrationBuilder.DropTable(
                name: "WorkOrderHistoryEvents");

            migrationBuilder.DropTable(
                name: "WorkOrderSubmissionAttachments");

            migrationBuilder.DropTable(
                name: "WorkOrderSubmissionChecklistResults");

            migrationBuilder.DropTable(
                name: "WorkOrderSubmissionDefects");

            migrationBuilder.DropTable(
                name: "WorkOrderSubmissionPartUsages");

            migrationBuilder.DropTable(
                name: "FileRecords");

            migrationBuilder.DropTable(
                name: "WorkOrderSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_SupervisorId_LifecycleStatus_SubmittedAt",
                table: "WorkOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WorkOrders_ExecutionCounters",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "HistoryVersion",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "SubmissionVersion",
                table: "WorkOrders");

        }
    }
}
