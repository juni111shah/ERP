using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HrWave4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EmployerCost",
                table: "hr_PayrollRuns",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "EmployerCost",
                table: "hr_PayrollLines",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "StatutoryDeduction",
                table: "hr_PayrollLines",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "hr_AttendanceLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    CheckIn = table.Column<TimeOnly>(type: "time", nullable: true),
                    CheckOut = table.Column<TimeOnly>(type: "time", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    WorkedMinutes = table.Column<int>(type: "int", nullable: false),
                    LateMinutes = table.Column<int>(type: "int", nullable: false),
                    OvertimeMinutes = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
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
                    table.PrimaryKey("PK_hr_AttendanceLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_AttendanceLogs_hr_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "hr_Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_ChecklistTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_hr_ChecklistTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "hr_EmployeeHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsPay = table.Column<bool>(type: "bit", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_hr_EmployeeHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_EmployeeHistory_hr_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "hr_Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_EmployeeTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsDone = table.Column<bool>(type: "bit", nullable: false),
                    DoneAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DoneByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_hr_EmployeeTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_EmployeeTasks_hr_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "hr_Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_JobOpenings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PositionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Headcount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ClosingDate = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_hr_JobOpenings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "hr_PayrollLineStatutory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    BaseAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
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
                    table.PrimaryKey("PK_hr_PayrollLineStatutory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "hr_StatutoryRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    Base = table.Column<int>(type: "int", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Threshold = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Cap = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Nationalities = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_hr_StatutoryRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "hr_ChecklistTemplateItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DueOffsetDays = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_hr_ChecklistTemplateItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_ChecklistTemplateItems_hr_ChecklistTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "hr_ChecklistTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_Candidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OpeningId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Stage = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExpectedSalary = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    StageChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_hr_Candidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_Candidates_hr_JobOpenings_OpeningId",
                        column: x => x.OpeningId,
                        principalTable: "hr_JobOpenings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "hr_StatutorySlabs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
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
                    table.PrimaryKey("PK_hr_StatutorySlabs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_hr_StatutorySlabs_hr_StatutoryRules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "hr_StatutoryRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_hr_AttendanceLogs_EmployeeId",
                table: "hr_AttendanceLogs",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_AttendanceLogs_OrgId_EmployeeId_Date",
                table: "hr_AttendanceLogs",
                columns: new[] { "OrgId", "EmployeeId", "Date" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_hr_AttendanceLogs_OrgId_LegalEntityId_Date",
                table: "hr_AttendanceLogs",
                columns: new[] { "OrgId", "LegalEntityId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_Candidates_OpeningId",
                table: "hr_Candidates",
                column: "OpeningId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_Candidates_OrgId_OpeningId_Stage",
                table: "hr_Candidates",
                columns: new[] { "OrgId", "OpeningId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_ChecklistTemplateItems_OrgId_TemplateId",
                table: "hr_ChecklistTemplateItems",
                columns: new[] { "OrgId", "TemplateId" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_ChecklistTemplateItems_TemplateId",
                table: "hr_ChecklistTemplateItems",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_ChecklistTemplates_OrgId_Name",
                table: "hr_ChecklistTemplates",
                columns: new[] { "OrgId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_hr_EmployeeHistory_EmployeeId",
                table: "hr_EmployeeHistory",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_EmployeeHistory_OrgId_EmployeeId_EffectiveDate",
                table: "hr_EmployeeHistory",
                columns: new[] { "OrgId", "EmployeeId", "EffectiveDate" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_EmployeeTasks_EmployeeId",
                table: "hr_EmployeeTasks",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_hr_EmployeeTasks_OrgId_EmployeeId",
                table: "hr_EmployeeTasks",
                columns: new[] { "OrgId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_EmployeeTasks_OrgId_IsDone_DueDate",
                table: "hr_EmployeeTasks",
                columns: new[] { "OrgId", "IsDone", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_JobOpenings_OrgId_Status",
                table: "hr_JobOpenings",
                columns: new[] { "OrgId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_PayrollLineStatutory_OrgId_PayrollRunId",
                table: "hr_PayrollLineStatutory",
                columns: new[] { "OrgId", "PayrollRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_PayrollLineStatutory_OrgId_RuleId",
                table: "hr_PayrollLineStatutory",
                columns: new[] { "OrgId", "RuleId" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_StatutoryRules_OrgId_LegalEntityId_Code",
                table: "hr_StatutoryRules",
                columns: new[] { "OrgId", "LegalEntityId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_hr_StatutorySlabs_OrgId_RuleId",
                table: "hr_StatutorySlabs",
                columns: new[] { "OrgId", "RuleId" });

            migrationBuilder.CreateIndex(
                name: "IX_hr_StatutorySlabs_RuleId",
                table: "hr_StatutorySlabs",
                column: "RuleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hr_AttendanceLogs");

            migrationBuilder.DropTable(
                name: "hr_Candidates");

            migrationBuilder.DropTable(
                name: "hr_ChecklistTemplateItems");

            migrationBuilder.DropTable(
                name: "hr_EmployeeHistory");

            migrationBuilder.DropTable(
                name: "hr_EmployeeTasks");

            migrationBuilder.DropTable(
                name: "hr_PayrollLineStatutory");

            migrationBuilder.DropTable(
                name: "hr_StatutorySlabs");

            migrationBuilder.DropTable(
                name: "hr_JobOpenings");

            migrationBuilder.DropTable(
                name: "hr_ChecklistTemplates");

            migrationBuilder.DropTable(
                name: "hr_StatutoryRules");

            migrationBuilder.DropColumn(
                name: "EmployerCost",
                table: "hr_PayrollRuns");

            migrationBuilder.DropColumn(
                name: "EmployerCost",
                table: "hr_PayrollLines");

            migrationBuilder.DropColumn(
                name: "StatutoryDeduction",
                table: "hr_PayrollLines");
        }
    }
}
