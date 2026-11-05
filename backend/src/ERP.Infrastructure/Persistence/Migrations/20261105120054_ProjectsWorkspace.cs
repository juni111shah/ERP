using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProjectsWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "BaselineEnd",
                table: "prj_Tasks",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "BaselineStart",
                table: "prj_Tasks",
                type: "date",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SignatureImage",
                table: "prj_Acceptances",
                type: "nvarchar(max)",
                maxLength: 300000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(400)",
                oldMaxLength: 400,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "prj_BudgetLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Hours = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_prj_BudgetLines", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_prj_BudgetLines_OrgId_ProjectId_Category",
                table: "prj_BudgetLines",
                columns: new[] { "OrgId", "ProjectId", "Category" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "prj_BudgetLines");

            migrationBuilder.DropColumn(
                name: "BaselineEnd",
                table: "prj_Tasks");

            migrationBuilder.DropColumn(
                name: "BaselineStart",
                table: "prj_Tasks");

            migrationBuilder.AlterColumn<string>(
                name: "SignatureImage",
                table: "prj_Acceptances",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldMaxLength: 300000,
                oldNullable: true);
        }
    }
}
