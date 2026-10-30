using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PosOneOpenSessionPerRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UX_pos_Sessions_OneOpenPerRegister",
                table: "pos_Sessions",
                columns: new[] { "OrgId", "RegisterId" },
                unique: true,
                filter: "[Status] = 0 AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_pos_Sessions_OneOpenPerRegister",
                table: "pos_Sessions");
        }
    }
}
