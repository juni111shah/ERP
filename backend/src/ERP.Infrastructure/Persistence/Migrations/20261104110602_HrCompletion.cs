using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HrCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_hr_PayrollRuns_OrgId_LegalEntityId_Year_Month",
                table: "hr_PayrollRuns");

            migrationBuilder.AddColumn<Guid>(
                name: "GeneratedByUserId",
                table: "hr_PayrollRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeSalary",
                table: "hr_PayrollRuns",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "hr_PayrollRuns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "LastAdjustedByUserId",
                table: "hr_PayrollRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PostedByUserId",
                table: "hr_PayrollRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "hr_PayrollRuns",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAt",
                table: "hr_PayrollRuns",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VoidedBy",
                table: "hr_PayrollRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PaidOn",
                table: "hr_PayrollLines",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentJournalId",
                table: "hr_PayrollLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProratedDays",
                table: "hr_PayrollLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AccrualMode",
                table: "hr_LeaveTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ProRataFirstYear",
                table: "hr_LeaveTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RequiresAttachmentAfterDays",
                table: "hr_LeaveTypes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EndHalfDay",
                table: "hr_LeaveRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "StartHalfDay",
                table: "hr_LeaveRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "hr_Employees",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "hr_Employees",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ContractEndDate",
                table: "hr_Employees",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContractType",
                table: "hr_Employees",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                table: "hr_Employees",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "hr_Employees",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmergencyContactName",
                table: "hr_Employees",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmergencyContactPhone",
                table: "hr_Employees",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "hr_Employees",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ManagerEmployeeId",
                table: "hr_Employees",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaritalStatus",
                table: "hr_Employees",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NoticePeriodDays",
                table: "hr_Employees",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PositionId",
                table: "hr_Employees",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ProbationEndDate",
                table: "hr_Employees",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "hr_Employees",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "hr_Departments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ParentDepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HeadEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BusinessUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_hr_Departments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "hr_EmployeeDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IssuedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_hr_EmployeeDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_EmployeeDocuments_hr_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "hr_Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_LeaveEntitlementTiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaveTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromServiceYears = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    Days = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_hr_LeaveEntitlementTiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_LeaveEntitlementTiers_hr_LeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "hr_LeaveTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_LeaveRequestYearDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Days = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_hr_LeaveRequestYearDays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "hr_Positions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Grade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_hr_Positions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_hr_PayrollRuns_OrgId_LegalEntityId_Year_Month",
                table: "hr_PayrollRuns",
                columns: new[] { "OrgId", "LegalEntityId", "Year", "Month" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Kind] = 0 AND [Status] <> 4");

            migrationBuilder.CreateIndex(
                name: "IX_hr_Employees_OrgId_DepartmentId",
                table: "hr_Employees",
                columns: new[] { "OrgId", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_Employees_OrgId_ManagerEmployeeId",
                table: "hr_Employees",
                columns: new[] { "OrgId", "ManagerEmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_Employees_OrgId_UserId",
                table: "hr_Employees",
                columns: new[] { "OrgId", "UserId" },
                unique: true,
                filter: "[UserId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_hr_Departments_OrgId_Code",
                table: "hr_Departments",
                columns: new[] { "OrgId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_hr_EmployeeDocuments_EmployeeId",
                table: "hr_EmployeeDocuments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_EmployeeDocuments_OrgId_EmployeeId",
                table: "hr_EmployeeDocuments",
                columns: new[] { "OrgId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_EmployeeDocuments_OrgId_ExpiryDate",
                table: "hr_EmployeeDocuments",
                columns: new[] { "OrgId", "ExpiryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_LeaveEntitlementTiers_LeaveTypeId",
                table: "hr_LeaveEntitlementTiers",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_LeaveEntitlementTiers_OrgId_LeaveTypeId",
                table: "hr_LeaveEntitlementTiers",
                columns: new[] { "OrgId", "LeaveTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_LeaveRequestYearDays_OrgId_RequestId",
                table: "hr_LeaveRequestYearDays",
                columns: new[] { "OrgId", "RequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_Positions_OrgId_Code",
                table: "hr_Positions",
                columns: new[] { "OrgId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            // Existing rows: payroll lines were always for the whole month, and lines of runs already paid were paid on the run's date.
            migrationBuilder.Sql(@"
UPDATE l SET ProratedDays = DAY(EOMONTH(DATEFROMPARTS(r.[Year], r.[Month], 1)))
FROM hr_PayrollLines l JOIN hr_PayrollRuns r ON r.Id = l.PayrollRunId
WHERE l.ProratedDays = 0;");
            migrationBuilder.Sql(@"
UPDATE l SET PaidOn = r.PaidOn, PaymentJournalId = r.PaymentJournalId
FROM hr_PayrollLines l JOIN hr_PayrollRuns r ON r.Id = l.PayrollRunId
WHERE r.[Status] = 3 AND l.PaidOn IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hr_Departments");

            migrationBuilder.DropTable(
                name: "hr_EmployeeDocuments");

            migrationBuilder.DropTable(
                name: "hr_LeaveEntitlementTiers");

            migrationBuilder.DropTable(
                name: "hr_LeaveRequestYearDays");

            migrationBuilder.DropTable(
                name: "hr_Positions");

            migrationBuilder.DropIndex(
                name: "IX_hr_PayrollRuns_OrgId_LegalEntityId_Year_Month",
                table: "hr_PayrollRuns");

            migrationBuilder.DropIndex(
                name: "IX_hr_Employees_OrgId_DepartmentId",
                table: "hr_Employees");

            migrationBuilder.DropIndex(
                name: "IX_hr_Employees_OrgId_ManagerEmployeeId",
                table: "hr_Employees");

            migrationBuilder.DropIndex(
                name: "IX_hr_Employees_OrgId_UserId",
                table: "hr_Employees");

            migrationBuilder.DropColumn(
                name: "GeneratedByUserId",
                table: "hr_PayrollRuns");

            migrationBuilder.DropColumn(
                name: "IncludeSalary",
                table: "hr_PayrollRuns");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "hr_PayrollRuns");

            migrationBuilder.DropColumn(
                name: "LastAdjustedByUserId",
                table: "hr_PayrollRuns");

            migrationBuilder.DropColumn(
                name: "PostedByUserId",
                table: "hr_PayrollRuns");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "hr_PayrollRuns");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                table: "hr_PayrollRuns");

            migrationBuilder.DropColumn(
                name: "VoidedBy",
                table: "hr_PayrollRuns");

            migrationBuilder.DropColumn(
                name: "PaidOn",
                table: "hr_PayrollLines");

            migrationBuilder.DropColumn(
                name: "PaymentJournalId",
                table: "hr_PayrollLines");

            migrationBuilder.DropColumn(
                name: "ProratedDays",
                table: "hr_PayrollLines");

            migrationBuilder.DropColumn(
                name: "AccrualMode",
                table: "hr_LeaveTypes");

            migrationBuilder.DropColumn(
                name: "ProRataFirstYear",
                table: "hr_LeaveTypes");

            migrationBuilder.DropColumn(
                name: "RequiresAttachmentAfterDays",
                table: "hr_LeaveTypes");

            migrationBuilder.DropColumn(
                name: "EndHalfDay",
                table: "hr_LeaveRequests");

            migrationBuilder.DropColumn(
                name: "StartHalfDay",
                table: "hr_LeaveRequests");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "hr_Employees");

            migrationBuilder.DropColumn(
                name: "BankName",
                table: "hr_Employees");

            migrationBuilder.DropColumn(
                name: "ContractEndDate",
                table: "hr_Employees");

            migrationBuilder.DropColumn(
                name: "ContractType",
                table: "hr_Employees");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "hr_Employees");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "hr_Employees");

            migrationBuilder.DropColumn(
                name: "EmergencyContactName",
                table: "hr_Employees");

            migrationBuilder.DropColumn(
                name: "EmergencyContactPhone",
                table: "hr_Employees");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "hr_Employees");

            migrationBuilder.DropColumn(
                name: "ManagerEmployeeId",
                table: "hr_Employees");

            migrationBuilder.DropColumn(
                name: "MaritalStatus",
                table: "hr_Employees");

            migrationBuilder.DropColumn(
                name: "NoticePeriodDays",
                table: "hr_Employees");

            migrationBuilder.DropColumn(
                name: "PositionId",
                table: "hr_Employees");

            migrationBuilder.DropColumn(
                name: "ProbationEndDate",
                table: "hr_Employees");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "hr_Employees");

            migrationBuilder.CreateIndex(
                name: "IX_hr_PayrollRuns_OrgId_LegalEntityId_Year_Month",
                table: "hr_PayrollRuns",
                columns: new[] { "OrgId", "LegalEntityId", "Year", "Month" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
