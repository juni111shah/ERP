using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlatformLegalDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "TermsAcceptedAt",
                table: "SignupRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LegalUpdatedAt",
                table: "PlatformSettings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrivacyText",
                table: "PlatformSettings",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TermsText",
                table: "PlatformSettings",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TermsAcceptedAt",
                table: "SignupRequests");

            migrationBuilder.DropColumn(
                name: "LegalUpdatedAt",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "PrivacyText",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "TermsText",
                table: "PlatformSettings");
        }
    }
}
