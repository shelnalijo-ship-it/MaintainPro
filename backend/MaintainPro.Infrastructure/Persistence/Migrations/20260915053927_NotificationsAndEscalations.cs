using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintainPro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NotificationsAndEscalations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssignmentVersion",
                table: "WorkOrders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CurrentTechnicianEmployeeId",
                table: "WorkOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentTechnicianName",
                table: "WorkOrders",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EscalationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    DueSoonDays = table.Column<int>(type: "integer", nullable: false),
                    TechnicianOverdueDays = table.Column<int>(type: "integer", nullable: false),
                    SupervisorEscalationDays = table.Column<int>(type: "integer", nullable: false),
                    ManagerEscalationDays = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscalationSettings", x => x.Id);
                    table.CheckConstraint("CK_EscalationSettings_SingletonAndThresholds", "\"Id\" = 1 AND \"DueSoonDays\" BETWEEN 0 AND 365 AND \"TechnicianOverdueDays\" >= 0 AND \"SupervisorEscalationDays\" >= \"TechnicianOverdueDays\" AND \"ManagerEscalationDays\" >= \"SupervisorEscalationDays\" AND \"ManagerEscalationDays\" <= 36500");
                    table.ForeignKey(
                        name: "FK_EscalationSettings_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotificationEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    Version = table.Column<Guid>(type: "uuid", nullable: false),
                    EscalationLevel = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationEvents", x => x.Id);
                    table.CheckConstraint("CK_NotificationEvents_Level", "\"EscalationLevel\" BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_NotificationEvents_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationType = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<string>(type: "text", nullable: false),
                    DeduplicationKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.CheckConstraint("CK_Notifications_ReadState", "(\"IsRead\" AND \"ReadAt\" IS NOT NULL) OR (NOT \"IsRead\" AND \"ReadAt\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_Notifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotificationDeliveryAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    AttemptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    ExternalMessageId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationDeliveryAttempts", x => x.Id);
                    table.CheckConstraint("CK_NotificationDeliveryAttempts_AttemptNumber", "\"AttemptNumber\" > 0");
                    table.ForeignKey(
                        name: "FK_NotificationDeliveryAttempts_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderEscalations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    TriggerType = table.Column<string>(type: "text", nullable: false),
                    TriggeredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeduplicationKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RoutingReason = table.Column<string>(type: "text", nullable: true),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderEscalations", x => x.Id);
                    table.CheckConstraint("CK_WorkOrderEscalations_Level", "\"Level\" BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_WorkOrderEscalations_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderEscalations_Users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderEscalations_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_WorkOrders_AssignmentVersion",
                table: "WorkOrders",
                sql: "\"AssignmentVersion\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationSettings_UpdatedByUserId",
                table: "EscalationSettings",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveryAttempts_NotificationId_Channel_Attempt~",
                table: "NotificationDeliveryAttempts",
                columns: new[] { "NotificationId", "Channel", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationEvents_DeduplicationKey",
                table: "NotificationEvents",
                column: "DeduplicationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationEvents_ProcessedAt_WorkOrderId",
                table: "NotificationEvents",
                columns: new[] { "ProcessedAt", "WorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationEvents_WorkOrderId",
                table: "NotificationEvents",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_DeduplicationKey",
                table: "Notifications",
                column: "DeduplicationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderEscalations_DeduplicationKey",
                table: "WorkOrderEscalations",
                column: "DeduplicationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderEscalations_NotificationId",
                table: "WorkOrderEscalations",
                column: "NotificationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderEscalations_RecipientUserId",
                table: "WorkOrderEscalations",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderEscalations_WorkOrderId_ResolvedAt_Level",
                table: "WorkOrderEscalations",
                columns: new[] { "WorkOrderId", "ResolvedAt", "Level" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EscalationSettings");

            migrationBuilder.DropTable(
                name: "NotificationDeliveryAttempts");

            migrationBuilder.DropTable(
                name: "NotificationEvents");

            migrationBuilder.DropTable(
                name: "WorkOrderEscalations");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WorkOrders_AssignmentVersion",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "AssignmentVersion",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "CurrentTechnicianEmployeeId",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "CurrentTechnicianName",
                table: "WorkOrders");
        }
    }
}
