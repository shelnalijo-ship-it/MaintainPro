using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintainPro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PreventivePlanningAndWorkOrderGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaintenanceTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderNumberSequences",
                columns: table => new
                {
                    Year = table.Column<int>(type: "integer", nullable: false),
                    LastValue = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderNumberSequences", x => x.Year);
                    table.CheckConstraint("CK_WorkOrderNumberSequences_Value", "\"LastValue\" >= 0");
                    table.CheckConstraint("CK_WorkOrderNumberSequences_Year", "\"Year\" >= 1 AND \"Year\" <= 9999");
                });

            migrationBuilder.CreateTable(
                name: "MaintenancePlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MachineId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanName = table.Column<string>(type: "text", nullable: false),
                    MaintenanceTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Priority = table.Column<string>(type: "text", nullable: false),
                    FrequencyType = table.Column<string>(type: "text", nullable: false),
                    FrequencyValue = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    NextDueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DefaultTechnicianId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupervisorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Instructions = table.Column<string>(type: "text", nullable: true),
                    EstimatedDurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    PhotoRequired = table.Column<bool>(type: "boolean", nullable: false),
                    MinimumPhotoCount = table.Column<int>(type: "integer", nullable: false),
                    CommentRequired = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenancePlans", x => x.Id);
                    table.CheckConstraint("CK_MaintenancePlans_Duration", "\"EstimatedDurationMinutes\" IS NULL OR \"EstimatedDurationMinutes\" >= 0");
                    table.CheckConstraint("CK_MaintenancePlans_Evidence", "(\"PhotoRequired\" AND \"MinimumPhotoCount\" >= 1) OR (NOT \"PhotoRequired\" AND \"MinimumPhotoCount\" = 0)");
                    table.CheckConstraint("CK_MaintenancePlans_Frequency", "\"FrequencyValue\" > 0");
                    table.ForeignKey(
                        name: "FK_MaintenancePlans_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenancePlans_MaintenanceTypes_MaintenanceTypeId",
                        column: x => x.MaintenanceTypeId,
                        principalTable: "MaintenanceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenancePlans_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenancePlans_Users_DefaultTechnicianId",
                        column: x => x.DefaultTechnicianId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenancePlans_Users_SupervisorId",
                        column: x => x.SupervisorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChecklistTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaintenancePlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistTemplates", x => x.Id);
                    table.CheckConstraint("CK_ChecklistTemplates_Version", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_ChecklistTemplates_MaintenancePlans_MaintenancePlanId",
                        column: x => x.MaintenancePlanId,
                        principalTable: "MaintenancePlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChecklistTemplates_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderNumber = table.Column<string>(type: "text", nullable: false),
                    MachineId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaintenancePlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedTechnicianId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupervisorId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlannedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Priority = table.Column<string>(type: "text", nullable: false),
                    LifecycleStatus = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EscalationLevel = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrders", x => x.Id);
                    table.CheckConstraint("CK_WorkOrders_Dates", "\"DueDate\" >= \"PlannedDate\"");
                    table.CheckConstraint("CK_WorkOrders_Escalation", "\"EscalationLevel\" >= 0");
                    table.ForeignKey(
                        name: "FK_WorkOrders_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrders_MaintenancePlans_MaintenancePlanId",
                        column: x => x.MaintenancePlanId,
                        principalTable: "MaintenancePlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrders_Users_AssignedTechnicianId",
                        column: x => x.AssignedTechnicianId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrders_Users_SupervisorId",
                        column: x => x.SupervisorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChecklistItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChecklistTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ResponseType = table.Column<string>(type: "text", nullable: false),
                    IsMandatory = table.Column<bool>(type: "boolean", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: true),
                    MinimumValue = table.Column<decimal>(type: "numeric", nullable: true),
                    MaximumValue = table.Column<decimal>(type: "numeric", nullable: true),
                    PhotoRequired = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistItems", x => x.Id);
                    table.CheckConstraint("CK_ChecklistItems_Bounds", "\"MinimumValue\" IS NULL OR \"MaximumValue\" IS NULL OR \"MinimumValue\" <= \"MaximumValue\"");
                    table.CheckConstraint("CK_ChecklistItems_Sequence", "\"SequenceNumber\" > 0");
                    table.ForeignKey(
                        name: "FK_ChecklistItems_ChecklistTemplates_ChecklistTemplateId",
                        column: x => x.ChecklistTemplateId,
                        principalTable: "ChecklistTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaintenanceTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChecklistTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChecklistVersion = table.Column<int>(type: "integer", nullable: false),
                    ChecklistName = table.Column<string>(type: "text", nullable: false),
                    PlanName = table.Column<string>(type: "text", nullable: false),
                    MaintenanceTypeName = table.Column<string>(type: "text", nullable: false),
                    Instructions = table.Column<string>(type: "text", nullable: true),
                    EstimatedDurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    PhotoRequired = table.Column<bool>(type: "boolean", nullable: false),
                    MinimumPhotoCount = table.Column<int>(type: "integer", nullable: false),
                    CommentRequired = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<string>(type: "text", nullable: false),
                    MachineCode = table.Column<string>(type: "text", nullable: false),
                    MachineName = table.Column<string>(type: "text", nullable: false),
                    AssignedTechnicianEmployeeId = table.Column<string>(type: "text", nullable: true),
                    AssignedTechnicianName = table.Column<string>(type: "text", nullable: true),
                    SupervisorEmployeeId = table.Column<string>(type: "text", nullable: false),
                    SupervisorName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderDefinitions_ChecklistTemplates_ChecklistTemplateId",
                        column: x => x.ChecklistTemplateId,
                        principalTable: "ChecklistTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderDefinitions_MaintenanceTypes_MaintenanceTypeId",
                        column: x => x.MaintenanceTypeId,
                        principalTable: "MaintenanceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderDefinitions_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderChecklistItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ResponseType = table.Column<string>(type: "text", nullable: false),
                    IsMandatory = table.Column<bool>(type: "boolean", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: true),
                    MinimumValue = table.Column<decimal>(type: "numeric", nullable: true),
                    MaximumValue = table.Column<decimal>(type: "numeric", nullable: true),
                    PhotoRequired = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderChecklistItems", x => x.Id);
                    table.CheckConstraint("CK_WorkOrderChecklistItems_Bounds", "\"MinimumValue\" IS NULL OR \"MaximumValue\" IS NULL OR \"MinimumValue\" <= \"MaximumValue\"");
                    table.CheckConstraint("CK_WorkOrderChecklistItems_Sequence", "\"SequenceNumber\" > 0");
                    table.ForeignKey(
                        name: "FK_WorkOrderChecklistItems_WorkOrderDefinitions_WorkOrderDefin~",
                        column: x => x.WorkOrderDefinitionId,
                        principalTable: "WorkOrderDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistItems_ChecklistTemplateId_SequenceNumber",
                table: "ChecklistItems",
                columns: new[] { "ChecklistTemplateId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistTemplates_CreatedByUserId",
                table: "ChecklistTemplates",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistTemplates_MaintenancePlanId_Version",
                table: "ChecklistTemplates",
                columns: new[] { "MaintenancePlanId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenancePlans_CreatedByUserId",
                table: "MaintenancePlans",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenancePlans_DefaultTechnicianId",
                table: "MaintenancePlans",
                column: "DefaultTechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenancePlans_IsActive_NextDueDate",
                table: "MaintenancePlans",
                columns: new[] { "IsActive", "NextDueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenancePlans_MachineId",
                table: "MaintenancePlans",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenancePlans_MaintenanceTypeId",
                table: "MaintenancePlans",
                column: "MaintenanceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenancePlans_SupervisorId",
                table: "MaintenancePlans",
                column: "SupervisorId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTypes_Name",
                table: "MaintenanceTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderChecklistItems_WorkOrderDefinitionId_SequenceNumber",
                table: "WorkOrderChecklistItems",
                columns: new[] { "WorkOrderDefinitionId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderDefinitions_ChecklistTemplateId",
                table: "WorkOrderDefinitions",
                column: "ChecklistTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderDefinitions_MaintenanceTypeId",
                table: "WorkOrderDefinitions",
                column: "MaintenanceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderDefinitions_WorkOrderId",
                table: "WorkOrderDefinitions",
                column: "WorkOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_AssignedTechnicianId_PlannedDate",
                table: "WorkOrders",
                columns: new[] { "AssignedTechnicianId", "PlannedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_LifecycleStatus_DueDate",
                table: "WorkOrders",
                columns: new[] { "LifecycleStatus", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_MachineId",
                table: "WorkOrders",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_MaintenancePlanId_PlannedDate",
                table: "WorkOrders",
                columns: new[] { "MaintenancePlanId", "PlannedDate" },
                unique: true,
                filter: "\"MaintenancePlanId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_SupervisorId",
                table: "WorkOrders",
                column: "SupervisorId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_WorkOrderNumber",
                table: "WorkOrders",
                column: "WorkOrderNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChecklistItems");

            migrationBuilder.DropTable(
                name: "WorkOrderChecklistItems");

            migrationBuilder.DropTable(
                name: "WorkOrderNumberSequences");

            migrationBuilder.DropTable(
                name: "WorkOrderDefinitions");

            migrationBuilder.DropTable(
                name: "ChecklistTemplates");

            migrationBuilder.DropTable(
                name: "WorkOrders");

            migrationBuilder.DropTable(
                name: "MaintenancePlans");

            migrationBuilder.DropTable(
                name: "MaintenanceTypes");
        }
    }
}
