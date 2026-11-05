using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProjectsSignature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EvidenceHash",
                table: "prj_Acceptances",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignatureImage",
                table: "prj_Acceptances",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignedIp",
                table: "prj_Acceptances",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EvidenceHash",
                table: "prj_Acceptances");

            migrationBuilder.DropColumn(
                name: "SignatureImage",
                table: "prj_Acceptances");

            migrationBuilder.DropColumn(
                name: "SignedIp",
                table: "prj_Acceptances");
        }
    }
}
