using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintainPro.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExternalServicesAndDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExternalServiceNumberSequences",
                columns: table => new
                {
                    Year = table.Column<int>(type: "integer", nullable: false),
                    LastValue = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalServiceNumberSequences", x => x.Year);
                    table.CheckConstraint("CK_ExternalServiceNumberSequences_Value", "\"LastValue\" >= 0");
                    table.CheckConstraint("CK_ExternalServiceNumberSequences_Year", "\"Year\" BETWEEN 1 AND 9999");
                });

            migrationBuilder.CreateTable(
                name: "ExternalServices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MachineId = table.Column<Guid>(type: "uuid", nullable: false),
                    MachineCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MachineName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ServiceCompany = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ServiceTechnician = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ServiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ServiceType = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    Findings = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    WorkCompleted = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    PartsReplaced = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    Cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    PurchaseOrderNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InvoiceNumber = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FollowUpDate = table.Column<DateOnly>(type: "date", nullable: true),
                    NextServiceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Recommendation = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    Comments = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalServices", x => x.Id);
                    table.CheckConstraint("CK_ExternalServices_Cost", "\"Cost\" IS NULL OR \"Cost\" >= 0");
                    table.CheckConstraint("CK_ExternalServices_Dates", "(\"FollowUpDate\" IS NULL OR \"FollowUpDate\" >= \"ServiceDate\") AND (\"NextServiceDate\" IS NULL OR \"NextServiceDate\" >= \"ServiceDate\")");
                    table.ForeignKey(
                        name: "FK_ExternalServices_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExternalServices_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MachineDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MachineId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DocumentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineDocuments", x => x.Id);
                    table.CheckConstraint("CK_MachineDocuments_Dates", "\"ExpiryDate\" IS NULL OR \"DocumentDate\" IS NULL OR \"ExpiryDate\" >= \"DocumentDate\"");
                    table.ForeignKey(
                        name: "FK_MachineDocuments_FileRecords_FileId",
                        column: x => x.FileId,
                        principalTable: "FileRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MachineDocuments_Machines_MachineId",
                        column: x => x.MachineId,
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MachineDocuments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExternalServiceAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalServiceAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalServiceAttachments_ExternalServices_ExternalService~",
                        column: x => x.ExternalServiceId,
                        principalTable: "ExternalServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExternalServiceAttachments_FileRecords_FileId",
                        column: x => x.FileId,
                        principalTable: "FileRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExternalServiceAttachments_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalServiceAttachments_ExternalServiceId_FileId",
                table: "ExternalServiceAttachments",
                columns: new[] { "ExternalServiceId", "FileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalServiceAttachments_ExternalServiceId_IsActive",
                table: "ExternalServiceAttachments",
                columns: new[] { "ExternalServiceId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalServiceAttachments_FileId",
                table: "ExternalServiceAttachments",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalServiceAttachments_UploadedByUserId",
                table: "ExternalServiceAttachments",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalServices_CreatedByUserId",
                table: "ExternalServices",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalServices_FollowUpDate",
                table: "ExternalServices",
                column: "FollowUpDate");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalServices_MachineId_ServiceDate",
                table: "ExternalServices",
                columns: new[] { "MachineId", "ServiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalServices_NextServiceDate",
                table: "ExternalServices",
                column: "NextServiceDate");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalServices_ServiceCompany",
                table: "ExternalServices",
                column: "ServiceCompany");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalServices_ServiceNumber",
                table: "ExternalServices",
                column: "ServiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MachineDocuments_ExpiryDate",
                table: "MachineDocuments",
                column: "ExpiryDate");

            migrationBuilder.CreateIndex(
                name: "IX_MachineDocuments_FileId",
                table: "MachineDocuments",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineDocuments_MachineId_DocumentType_IsActive",
                table: "MachineDocuments",
                columns: new[] { "MachineId", "DocumentType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MachineDocuments_MachineId_FileId",
                table: "MachineDocuments",
                columns: new[] { "MachineId", "FileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MachineDocuments_UploadedAt",
                table: "MachineDocuments",
                column: "UploadedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MachineDocuments_UploadedByUserId",
                table: "MachineDocuments",
                column: "UploadedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalServiceAttachments");

            migrationBuilder.DropTable(
                name: "ExternalServiceNumberSequences");

            migrationBuilder.DropTable(
                name: "MachineDocuments");

            migrationBuilder.DropTable(
                name: "ExternalServices");
        }
    }
}
