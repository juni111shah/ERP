using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProjectsWave2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApprovalStatus",
                table: "prj_Timesheets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "CostAmount",
                table: "prj_Timesheets",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CostRate",
                table: "prj_Timesheets",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "DecidedAt",
                table: "prj_Timesheets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DecidedByUserId",
                table: "prj_Timesheets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectReason",
                table: "prj_Timesheets",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "prj_Timesheets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkerUserId",
                table: "prj_Timesheets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WrittenOff",
                table: "prj_Timesheets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedUserId",
                table: "prj_Tasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Labels",
                table: "prj_Tasks",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentTaskId",
                table: "prj_Tasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartDate",
                table: "prj_Tasks",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BudgetHours",
                table: "prj_Projects",
                type: "decimal(9,2)",
                precision: 9,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "prj_Projects",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTemplate",
                table: "prj_Projects",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ManagerUserId",
                table: "prj_Projects",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceId",
                table: "prj_Projects",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "prj_Projects",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TaxCodeId",
                table: "prj_Projects",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InvoiceId",
                table: "prj_Milestones",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "prj_Allocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    HoursPerWeek = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_prj_Allocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "prj_ChangeOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AmountChange = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    HoursChange = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_prj_ChangeOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "prj_Comments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AuthorName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
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
                    table.PrimaryKey("PK_prj_Comments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "prj_Expenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    IsBillable = table.Column<bool>(type: "bit", nullable: false),
                    MarkupPercent = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    WorkerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkerName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RejectReason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsBilled = table.Column<bool>(type: "bit", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WrittenOff = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_prj_Expenses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "prj_Members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_prj_Members", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "prj_ProgressBillings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Percent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Released = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_prj_ProgressBillings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "prj_Rates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Role = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BillRate = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CostRate = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
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
                    table.PrimaryKey("PK_prj_Rates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "prj_Risks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_prj_Risks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "prj_TaskDependencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DependsOnTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_prj_TaskDependencies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_prj_Timesheets_OrgId_WorkerUserId_Date",
                table: "prj_Timesheets",
                columns: new[] { "OrgId", "WorkerUserId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_prj_Tasks_OrgId_AssignedUserId",
                table: "prj_Tasks",
                columns: new[] { "OrgId", "AssignedUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_prj_Tasks_OrgId_ParentTaskId",
                table: "prj_Tasks",
                columns: new[] { "OrgId", "ParentTaskId" });

            migrationBuilder.CreateIndex(
                name: "IX_prj_Allocations_OrgId_UserId_FromDate",
                table: "prj_Allocations",
                columns: new[] { "OrgId", "UserId", "FromDate" });

            migrationBuilder.CreateIndex(
                name: "IX_prj_ChangeOrders_OrgId_ProjectId",
                table: "prj_ChangeOrders",
                columns: new[] { "OrgId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_prj_Comments_OrgId_OwnerType_OwnerId",
                table: "prj_Comments",
                columns: new[] { "OrgId", "OwnerType", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_prj_Expenses_OrgId_ProjectId_IsBilled",
                table: "prj_Expenses",
                columns: new[] { "OrgId", "ProjectId", "IsBilled" });

            migrationBuilder.CreateIndex(
                name: "IX_prj_Members_OrgId_ProjectId_UserId",
                table: "prj_Members",
                columns: new[] { "OrgId", "ProjectId", "UserId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_prj_ProgressBillings_OrgId_ProjectId",
                table: "prj_ProgressBillings",
                columns: new[] { "OrgId", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_prj_Rates_OrgId_ProjectId_UserId",
                table: "prj_Rates",
                columns: new[] { "OrgId", "ProjectId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_prj_Risks_OrgId_ProjectId_Status",
                table: "prj_Risks",
                columns: new[] { "OrgId", "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_prj_TaskDependencies_OrgId_TaskId_DependsOnTaskId",
                table: "prj_TaskDependencies",
                columns: new[] { "OrgId", "TaskId", "DependsOnTaskId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "prj_Allocations");

            migrationBuilder.DropTable(
                name: "prj_ChangeOrders");

            migrationBuilder.DropTable(
                name: "prj_Comments");

            migrationBuilder.DropTable(
                name: "prj_Expenses");

            migrationBuilder.DropTable(
                name: "prj_Members");

            migrationBuilder.DropTable(
                name: "prj_ProgressBillings");

            migrationBuilder.DropTable(
                name: "prj_Rates");

            migrationBuilder.DropTable(
                name: "prj_Risks");

            migrationBuilder.DropTable(
                name: "prj_TaskDependencies");

            migrationBuilder.DropIndex(
                name: "IX_prj_Timesheets_OrgId_WorkerUserId_Date",
                table: "prj_Timesheets");

            migrationBuilder.DropIndex(
                name: "IX_prj_Tasks_OrgId_AssignedUserId",
                table: "prj_Tasks");

            migrationBuilder.DropIndex(
                name: "IX_prj_Tasks_OrgId_ParentTaskId",
                table: "prj_Tasks");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "prj_Timesheets");

            migrationBuilder.DropColumn(
                name: "CostAmount",
                table: "prj_Timesheets");

            migrationBuilder.DropColumn(
                name: "CostRate",
                table: "prj_Timesheets");

            migrationBuilder.DropColumn(
                name: "DecidedAt",
                table: "prj_Timesheets");

            migrationBuilder.DropColumn(
                name: "DecidedByUserId",
                table: "prj_Timesheets");

            migrationBuilder.DropColumn(
                name: "RejectReason",
                table: "prj_Timesheets");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "prj_Timesheets");

            migrationBuilder.DropColumn(
                name: "WorkerUserId",
                table: "prj_Timesheets");

            migrationBuilder.DropColumn(
                name: "WrittenOff",
                table: "prj_Timesheets");

            migrationBuilder.DropColumn(
                name: "AssignedUserId",
                table: "prj_Tasks");

            migrationBuilder.DropColumn(
                name: "Labels",
                table: "prj_Tasks");

            migrationBuilder.DropColumn(
                name: "ParentTaskId",
                table: "prj_Tasks");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "prj_Tasks");

            migrationBuilder.DropColumn(
                name: "BudgetHours",
                table: "prj_Projects");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "prj_Projects");

            migrationBuilder.DropColumn(
                name: "IsTemplate",
                table: "prj_Projects");

            migrationBuilder.DropColumn(
                name: "ManagerUserId",
                table: "prj_Projects");

            migrationBuilder.DropColumn(
                name: "SourceId",
                table: "prj_Projects");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "prj_Projects");

            migrationBuilder.DropColumn(
                name: "TaxCodeId",
                table: "prj_Projects");

            migrationBuilder.DropColumn(
                name: "InvoiceId",
                table: "prj_Milestones");
        }
    }
}
