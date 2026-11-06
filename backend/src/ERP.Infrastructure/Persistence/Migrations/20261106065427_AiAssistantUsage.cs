using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiAssistantUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BrandAssistantDailyTokens",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 150000);

            migrationBuilder.AddColumn<int>(
                name: "WorkspaceAssistantDailyTokens",
                table: "PlatformSettings",
                type: "int",
                nullable: false,
                defaultValue: 50000);

            migrationBuilder.CreateTable(
                name: "AssistantUsageDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Bot = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProviderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Requests = table.Column<int>(type: "int", nullable: false),
                    Failures = table.Column<int>(type: "int", nullable: false),
                    InputTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: false),
                    LastFailure = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantUsageDays", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantUsageDays_Day_Bot_ProviderId",
                table: "AssistantUsageDays",
                columns: new[] { "Day", "Bot", "ProviderId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssistantUsageDays");

            migrationBuilder.DropColumn(
                name: "BrandAssistantDailyTokens",
                table: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "WorkspaceAssistantDailyTokens",
                table: "PlatformSettings");
        }
    }
}
