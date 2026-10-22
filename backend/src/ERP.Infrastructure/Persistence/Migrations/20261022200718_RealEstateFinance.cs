using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RealEstateFinance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreditNoteId",
                table: "re_RentInstalments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "AgentPaidOn",
                table: "re_BrokerageDeals",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AgentPayoutJournalId",
                table: "re_BrokerageDeals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CommissionInvoiceId",
                table: "re_BrokerageDeals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "re_DepositTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    JournalEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrgId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    LegalEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_re_DepositTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_re_DepositTransactions_re_Leases_LeaseId",
                        column: x => x.LeaseId,
                        principalTable: "re_Leases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_re_DepositTransactions_LeaseId",
                table: "re_DepositTransactions",
                column: "LeaseId");

            migrationBuilder.CreateIndex(
                name: "IX_re_DepositTransactions_OrgId_LeaseId_Date",
                table: "re_DepositTransactions",
                columns: new[] { "OrgId", "LeaseId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "re_DepositTransactions");

            migrationBuilder.DropColumn(
                name: "CreditNoteId",
                table: "re_RentInstalments");

            migrationBuilder.DropColumn(
                name: "AgentPaidOn",
                table: "re_BrokerageDeals");

            migrationBuilder.DropColumn(
                name: "AgentPayoutJournalId",
                table: "re_BrokerageDeals");

            migrationBuilder.DropColumn(
                name: "CommissionInvoiceId",
                table: "re_BrokerageDeals");
        }
    }
}
