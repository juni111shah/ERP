using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinanceReportingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Settlements_OrgId_LegalEntityId_Direction_Date",
                table: "Settlements",
                columns: new[] { "OrgId", "LegalEntityId", "Direction", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_OrgId_LegalEntityId_Status_Date",
                table: "JournalEntries",
                columns: new[] { "OrgId", "LegalEntityId", "Status", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_OrgId_LegalEntityId_Status_InvoiceDate",
                table: "Invoices",
                columns: new[] { "OrgId", "LegalEntityId", "Status", "InvoiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Bills_OrgId_LegalEntityId_Status_BillDate",
                table: "Bills",
                columns: new[] { "OrgId", "LegalEntityId", "Status", "BillDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Settlements_OrgId_LegalEntityId_Direction_Date",
                table: "Settlements");

            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_OrgId_LegalEntityId_Status_Date",
                table: "JournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_OrgId_LegalEntityId_Status_InvoiceDate",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Bills_OrgId_LegalEntityId_Status_BillDate",
                table: "Bills");
        }
    }
}
