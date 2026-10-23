using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HrExtras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankRoutingCode",
                table: "hr_Employees",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LabourCardNumber",
                table: "hr_Employees",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "hr_ReviewCycles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OrgId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_ReviewCycles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "hr_PerformanceReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CycleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    OverallRating = table.Column<int>(type: "int", nullable: true),
                    Goals = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OrgId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    LegalEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_PerformanceReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_PerformanceReviews_hr_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "hr_Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_hr_PerformanceReviews_hr_ReviewCycles_CycleId",
                        column: x => x.CycleId,
                        principalTable: "hr_ReviewCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_ReviewCompetencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hr_ReviewCompetencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_ReviewCompetencies_hr_PerformanceReviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "hr_PerformanceReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_hr_PerformanceReviews_CycleId",
                table: "hr_PerformanceReviews",
                column: "CycleId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_PerformanceReviews_EmployeeId",
                table: "hr_PerformanceReviews",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_PerformanceReviews_OrgId_EmployeeId_CycleId",
                table: "hr_PerformanceReviews",
                columns: new[] { "OrgId", "EmployeeId", "CycleId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_hr_ReviewCompetencies_ReviewId",
                table: "hr_ReviewCompetencies",
                column: "ReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_ReviewCycles_OrgId_Name",
                table: "hr_ReviewCycles",
                columns: new[] { "OrgId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hr_ReviewCompetencies");

            migrationBuilder.DropTable(
                name: "hr_PerformanceReviews");

            migrationBuilder.DropTable(
                name: "hr_ReviewCycles");

            migrationBuilder.DropColumn(
                name: "BankRoutingCode",
                table: "hr_Employees");

            migrationBuilder.DropColumn(
                name: "LabourCardNumber",
                table: "hr_Employees");
        }
    }
}
