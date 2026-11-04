using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HrWave1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EndOfService",
                table: "hr_PayrollLines",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LeaveEncashment",
                table: "hr_PayrollLines",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LoanRecovery",
                table: "hr_PayrollLines",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "Encashable",
                table: "hr_LeaveTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastWorkingDay",
                table: "hr_Employees",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "NoticeGivenOn",
                table: "hr_Employees",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TerminationReason",
                table: "hr_Employees",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "hr_EmployeeLoans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Principal = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    InstalmentAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    StartYear = table.Column<int>(type: "int", nullable: false),
                    StartMonth = table.Column<int>(type: "int", nullable: false),
                    Instalments = table.Column<int>(type: "int", nullable: false),
                    Remaining = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DisbursedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    DisbursementJournalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_hr_EmployeeLoans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_EmployeeLoans_hr_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "hr_Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_LeavePayBands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaveTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromDay = table.Column<int>(type: "int", nullable: false),
                    PayPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_hr_LeavePayBands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_LeavePayBands_hr_LeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "hr_LeaveTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_PayrollLoanRecoveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Applied = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_hr_PayrollLoanRecoveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_PayrollLoanRecoveries_hr_EmployeeLoans_LoanId",
                        column: x => x.LoanId,
                        principalTable: "hr_EmployeeLoans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_hr_EmployeeLoans_EmployeeId",
                table: "hr_EmployeeLoans",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_EmployeeLoans_OrgId_EmployeeId_Status",
                table: "hr_EmployeeLoans",
                columns: new[] { "OrgId", "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_LeavePayBands_LeaveTypeId",
                table: "hr_LeavePayBands",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_LeavePayBands_OrgId_LeaveTypeId",
                table: "hr_LeavePayBands",
                columns: new[] { "OrgId", "LeaveTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_PayrollLoanRecoveries_LoanId",
                table: "hr_PayrollLoanRecoveries",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_PayrollLoanRecoveries_OrgId_LoanId",
                table: "hr_PayrollLoanRecoveries",
                columns: new[] { "OrgId", "LoanId" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_PayrollLoanRecoveries_OrgId_PayrollRunId",
                table: "hr_PayrollLoanRecoveries",
                columns: new[] { "OrgId", "PayrollRunId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hr_LeavePayBands");

            migrationBuilder.DropTable(
                name: "hr_PayrollLoanRecoveries");

            migrationBuilder.DropTable(
                name: "hr_EmployeeLoans");

            migrationBuilder.DropColumn(
                name: "EndOfService",
                table: "hr_PayrollLines");

            migrationBuilder.DropColumn(
                name: "LeaveEncashment",
                table: "hr_PayrollLines");

            migrationBuilder.DropColumn(
                name: "LoanRecovery",
                table: "hr_PayrollLines");

            migrationBuilder.DropColumn(
                name: "Encashable",
                table: "hr_LeaveTypes");

            migrationBuilder.DropColumn(
                name: "LastWorkingDay",
                table: "hr_Employees");

            migrationBuilder.DropColumn(
                name: "NoticeGivenOn",
                table: "hr_Employees");

            migrationBuilder.DropColumn(
                name: "TerminationReason",
                table: "hr_Employees");
        }
    }
}
