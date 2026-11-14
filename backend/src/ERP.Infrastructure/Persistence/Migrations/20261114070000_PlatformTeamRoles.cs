using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlatformTeamRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlatformRole",
                table: "Users",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Support");

            // The team had one level until now: everyone already on it keeps what they could do.
            migrationBuilder.Sql("UPDATE [Users] SET [PlatformRole] = N'Manager' WHERE [IsPlatformAdmin] = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlatformRole",
                table: "Users");
        }
    }
}
