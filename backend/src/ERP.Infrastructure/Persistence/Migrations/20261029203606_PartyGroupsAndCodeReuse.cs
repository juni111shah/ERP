using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PartyGroupsAndCodeReuse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Accounts_OrgId_Code",
                table: "Accounts");

            migrationBuilder.AddColumn<Guid>(
                name: "PartyGroupId",
                table: "Parties",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PartyGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_PartyGroups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Parties_OrgId_PartyGroupId",
                table: "Parties",
                columns: new[] { "OrgId", "PartyGroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_Parties_PartyGroupId",
                table: "Parties",
                column: "PartyGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_OrgId_Code",
                table: "Accounts",
                columns: new[] { "OrgId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PartyGroups_OrgId_Code",
                table: "PartyGroups",
                columns: new[] { "OrgId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_Parties_PartyGroups_PartyGroupId",
                table: "Parties",
                column: "PartyGroupId",
                principalTable: "PartyGroups",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Parties_PartyGroups_PartyGroupId",
                table: "Parties");

            migrationBuilder.DropTable(
                name: "PartyGroups");

            migrationBuilder.DropIndex(
                name: "IX_Parties_OrgId_PartyGroupId",
                table: "Parties");

            migrationBuilder.DropIndex(
                name: "IX_Parties_PartyGroupId",
                table: "Parties");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_OrgId_Code",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "PartyGroupId",
                table: "Parties");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_OrgId_Code",
                table: "Accounts",
                columns: new[] { "OrgId", "Code" },
                unique: true);
        }
    }
}
