using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiAssistantFlows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WorkspaceAssistantDailyMessagesPerClient",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 200);

            migrationBuilder.AddColumn<bool>(
                name: "Helpful",
                table: "BrandMessages",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "ast_PendingActions",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "LegalEntityId",
                table: "ast_PendingActions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Helpful",
                table: "ast_Messages",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsNote",
                table: "ast_Messages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ConsecutiveFailures",
                table: "AiProviders",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WorkspaceAssistantDailyMessagesPerClient",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "Helpful",
                table: "BrandMessages");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "ast_PendingActions");

            migrationBuilder.DropColumn(
                name: "LegalEntityId",
                table: "ast_PendingActions");

            migrationBuilder.DropColumn(
                name: "Helpful",
                table: "ast_Messages");

            migrationBuilder.DropColumn(
                name: "IsNote",
                table: "ast_Messages");

            migrationBuilder.DropColumn(
                name: "ConsecutiveFailures",
                table: "AiProviders");
        }
    }
}
