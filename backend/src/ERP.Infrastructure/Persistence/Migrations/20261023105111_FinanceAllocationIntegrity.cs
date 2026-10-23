using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinanceAllocationIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bills_OrgId_PartyId_VendorReference",
                table: "Bills");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementAllocation_BillId",
                table: "SettlementAllocation",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementAllocation_InvoiceId",
                table: "SettlementAllocation",
                column: "InvoiceId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SettlementAllocation_Amount",
                table: "SettlementAllocation",
                sql: "[Amount] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SettlementAllocation_ExactlyOneDoc",
                table: "SettlementAllocation",
                sql: "([InvoiceId] IS NOT NULL AND [BillId] IS NULL) OR ([InvoiceId] IS NULL AND [BillId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_OrgId_PartyId_VendorReference",
                table: "Bills",
                columns: new[] { "OrgId", "PartyId", "VendorReference" },
                unique: true,
                filter: "[VendorReference] IS NOT NULL AND [VendorReference] <> N'' AND [IsDeleted] = 0 AND [Status] <> N'Voided'");

            migrationBuilder.AddForeignKey(
                name: "FK_SettlementAllocation_Bills_BillId",
                table: "SettlementAllocation",
                column: "BillId",
                principalTable: "Bills",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SettlementAllocation_Invoices_InvoiceId",
                table: "SettlementAllocation",
                column: "InvoiceId",
                principalTable: "Invoices",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SettlementAllocation_Bills_BillId",
                table: "SettlementAllocation");

            migrationBuilder.DropForeignKey(
                name: "FK_SettlementAllocation_Invoices_InvoiceId",
                table: "SettlementAllocation");

            migrationBuilder.DropIndex(
                name: "IX_SettlementAllocation_BillId",
                table: "SettlementAllocation");

            migrationBuilder.DropIndex(
                name: "IX_SettlementAllocation_InvoiceId",
                table: "SettlementAllocation");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SettlementAllocation_Amount",
                table: "SettlementAllocation");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SettlementAllocation_ExactlyOneDoc",
                table: "SettlementAllocation");

            migrationBuilder.DropIndex(
                name: "IX_Bills_OrgId_PartyId_VendorReference",
                table: "Bills");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_OrgId_PartyId_VendorReference",
                table: "Bills",
                columns: new[] { "OrgId", "PartyId", "VendorReference" });
        }
    }
}
