using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HrShifts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ShiftId",
                table: "hr_Employees",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "hr_Shifts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    GraceMinutes = table.Column<int>(type: "int", nullable: false),
                    HoursPerDay = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_hr_Shifts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_hr_Shifts_OrgId_Code",
                table: "hr_Shifts",
                columns: new[] { "OrgId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hr_Shifts");

            migrationBuilder.DropColumn(
                name: "ShiftId",
                table: "hr_Employees");
        }
    }
}
