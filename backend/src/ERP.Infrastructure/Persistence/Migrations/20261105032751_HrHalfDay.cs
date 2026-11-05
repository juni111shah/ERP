using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HrHalfDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "UnpaidAbsentDays",
                table: "hr_AttendanceRecords",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "UnpaidAbsentDays",
                table: "hr_AttendanceRecords",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2);
        }
    }
}
