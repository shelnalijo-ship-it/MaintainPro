using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintainPro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReportingQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_DueDate",
                table: "WorkOrders",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_Machines_DepartmentId_LocationId_Status",
                table: "Machines",
                columns: new[] { "DepartmentId", "LocationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MachineAssignmentHistories_MachineId_EffectiveFrom",
                table: "MachineAssignmentHistories",
                columns: new[] { "MachineId", "EffectiveFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalServices_ServiceDate",
                table: "ExternalServices",
                column: "ServiceDate");

            migrationBuilder.CreateIndex(
                name: "IX_Breakdowns_ReportedAt",
                table: "Breakdowns",
                column: "ReportedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId_CreatedAt",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_DueDate",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_Machines_DepartmentId_LocationId_Status",
                table: "Machines");

            migrationBuilder.DropIndex(
                name: "IX_MachineAssignmentHistories_MachineId_EffectiveFrom",
                table: "MachineAssignmentHistories");

            migrationBuilder.DropIndex(
                name: "IX_ExternalServices_ServiceDate",
                table: "ExternalServices");

            migrationBuilder.DropIndex(
                name: "IX_Breakdowns_ReportedAt",
                table: "Breakdowns");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_EntityType_EntityId_CreatedAt",
                table: "AuditLogs");
        }
    }
}
