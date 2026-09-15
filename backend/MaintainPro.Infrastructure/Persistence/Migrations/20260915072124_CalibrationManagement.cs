using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintainPro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CalibrationManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalibrationCertificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MachineId = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CalibrationProvider = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CalibrationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Result = table.Column<string>(type: "text", nullable: false),
                    Remarks = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    CertificateFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalibrationCertificates", x => x.Id);
                    table.CheckConstraint("CK_CalibrationCertificates_Dates", "\"ExpiryDate\" > \"CalibrationDate\"");
                    table.ForeignKey(
                        name: "FK_CalibrationCertificates_FileRecords_CertificateFileId",
                        column: x => x.CertificateFileId,
                        principalTable: "FileRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalibrationCertificates_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalibrationCertificates_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CalibrationRenewals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MachineId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousCertificateId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedCertificateId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalibrationRenewals", x => x.Id);
                    table.CheckConstraint("CK_CalibrationRenewals_Completion", "(\"Status\" = 'IN_PROGRESS' AND \"CompletedAt\" IS NULL AND \"CompletedCertificateId\" IS NULL) OR (\"Status\" = 'COMPLETED' AND \"CompletedAt\" IS NOT NULL AND \"CompletedCertificateId\" IS NOT NULL) OR (\"Status\" = 'CANCELLED' AND \"CompletedAt\" IS NOT NULL AND \"CompletedCertificateId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_CalibrationRenewals_CalibrationCertificates_CompletedCertif~",
                        column: x => x.CompletedCertificateId,
                        principalTable: "CalibrationCertificates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalibrationRenewals_CalibrationCertificates_PreviousCertifi~",
                        column: x => x.PreviousCertificateId,
                        principalTable: "CalibrationCertificates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalibrationRenewals_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalibrationRenewals_Users_StartedByUserId",
                        column: x => x.StartedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CalibrationNotificationEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MachineId = table.Column<Guid>(type: "uuid", nullable: false),
                    CalibrationCertificateId = table.Column<Guid>(type: "uuid", nullable: true),
                    CalibrationRenewalId = table.Column<Guid>(type: "uuid", nullable: true),
                    NotificationType = table.Column<string>(type: "text", nullable: false),
                    Priority = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    DeduplicationKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalibrationNotificationEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalibrationNotificationEvents_CalibrationCertificates_Calib~",
                        column: x => x.CalibrationCertificateId,
                        principalTable: "CalibrationCertificates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalibrationNotificationEvents_CalibrationRenewals_Calibrati~",
                        column: x => x.CalibrationRenewalId,
                        principalTable: "CalibrationRenewals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalibrationNotificationEvents_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationCertificates_CalibrationProvider_CertificateNumb~",
                table: "CalibrationCertificates",
                columns: new[] { "CalibrationProvider", "CertificateNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationCertificates_CertificateFileId",
                table: "CalibrationCertificates",
                column: "CertificateFileId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationCertificates_CreatedByUserId",
                table: "CalibrationCertificates",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationCertificates_ExpiryDate",
                table: "CalibrationCertificates",
                column: "ExpiryDate");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationCertificates_MachineId_CalibrationDate_CreatedAt",
                table: "CalibrationCertificates",
                columns: new[] { "MachineId", "CalibrationDate", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationNotificationEvents_CalibrationCertificateId",
                table: "CalibrationNotificationEvents",
                column: "CalibrationCertificateId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationNotificationEvents_CalibrationRenewalId",
                table: "CalibrationNotificationEvents",
                column: "CalibrationRenewalId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationNotificationEvents_DeduplicationKey",
                table: "CalibrationNotificationEvents",
                column: "DeduplicationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationNotificationEvents_MachineId",
                table: "CalibrationNotificationEvents",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationNotificationEvents_ProcessedAt_MachineId",
                table: "CalibrationNotificationEvents",
                columns: new[] { "ProcessedAt", "MachineId" });

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationRenewals_CompletedCertificateId",
                table: "CalibrationRenewals",
                column: "CompletedCertificateId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationRenewals_MachineId",
                table: "CalibrationRenewals",
                column: "MachineId",
                unique: true,
                filter: "\"Status\" = 'IN_PROGRESS'");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationRenewals_MachineId_StartedAt",
                table: "CalibrationRenewals",
                columns: new[] { "MachineId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationRenewals_PreviousCertificateId",
                table: "CalibrationRenewals",
                column: "PreviousCertificateId");

            migrationBuilder.CreateIndex(
                name: "IX_CalibrationRenewals_StartedByUserId",
                table: "CalibrationRenewals",
                column: "StartedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalibrationNotificationEvents");

            migrationBuilder.DropTable(
                name: "CalibrationRenewals");

            migrationBuilder.DropTable(
                name: "CalibrationCertificates");
        }
    }
}
