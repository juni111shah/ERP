using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinanceReferentialFks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Settlements_BankAccountId",
                table: "Settlements",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Settlements_JournalEntryId",
                table: "Settlements",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_Settlements_LegalEntityId",
                table: "Settlements",
                column: "LegalEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Settlements_PartyId",
                table: "Settlements",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_Settlements_PaymentInstrumentId",
                table: "Settlements",
                column: "PaymentInstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_JournalEntryId",
                table: "Invoices",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_LegalEntityId",
                table: "Invoices",
                column: "LegalEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_PartyId",
                table: "Invoices",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLine_AccountId",
                table: "InvoiceLine",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLine_ItemId",
                table: "InvoiceLine",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLine_TaxCodeId",
                table: "InvoiceLine",
                column: "TaxCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_JournalEntryId",
                table: "Bills",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_LegalEntityId",
                table: "Bills",
                column: "LegalEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_PartyId",
                table: "Bills",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_BillLine_AccountId",
                table: "BillLine",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BillLine_ItemId",
                table: "BillLine",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_BillLine_TaxCodeId",
                table: "BillLine",
                column: "TaxCodeId");

            migrationBuilder.AddForeignKey(
                name: "FK_BillLine_Accounts_AccountId",
                table: "BillLine",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BillLine_Items_ItemId",
                table: "BillLine",
                column: "ItemId",
                principalTable: "Items",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BillLine_TaxCodes_TaxCodeId",
                table: "BillLine",
                column: "TaxCodeId",
                principalTable: "TaxCodes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Bills_JournalEntries_JournalEntryId",
                table: "Bills",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Bills_LegalEntities_LegalEntityId",
                table: "Bills",
                column: "LegalEntityId",
                principalTable: "LegalEntities",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Bills_Parties_PartyId",
                table: "Bills",
                column: "PartyId",
                principalTable: "Parties",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceLine_Accounts_AccountId",
                table: "InvoiceLine",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceLine_Items_ItemId",
                table: "InvoiceLine",
                column: "ItemId",
                principalTable: "Items",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceLine_TaxCodes_TaxCodeId",
                table: "InvoiceLine",
                column: "TaxCodeId",
                principalTable: "TaxCodes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_JournalEntries_JournalEntryId",
                table: "Invoices",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_LegalEntities_LegalEntityId",
                table: "Invoices",
                column: "LegalEntityId",
                principalTable: "LegalEntities",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Parties_PartyId",
                table: "Invoices",
                column: "PartyId",
                principalTable: "Parties",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Settlements_Accounts_BankAccountId",
                table: "Settlements",
                column: "BankAccountId",
                principalTable: "Accounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Settlements_JournalEntries_JournalEntryId",
                table: "Settlements",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Settlements_LegalEntities_LegalEntityId",
                table: "Settlements",
                column: "LegalEntityId",
                principalTable: "LegalEntities",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Settlements_Parties_PartyId",
                table: "Settlements",
                column: "PartyId",
                principalTable: "Parties",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Settlements_PaymentInstruments_PaymentInstrumentId",
                table: "Settlements",
                column: "PaymentInstrumentId",
                principalTable: "PaymentInstruments",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BillLine_Accounts_AccountId",
                table: "BillLine");

            migrationBuilder.DropForeignKey(
                name: "FK_BillLine_Items_ItemId",
                table: "BillLine");

            migrationBuilder.DropForeignKey(
                name: "FK_BillLine_TaxCodes_TaxCodeId",
                table: "BillLine");

            migrationBuilder.DropForeignKey(
                name: "FK_Bills_JournalEntries_JournalEntryId",
                table: "Bills");

            migrationBuilder.DropForeignKey(
                name: "FK_Bills_LegalEntities_LegalEntityId",
                table: "Bills");

            migrationBuilder.DropForeignKey(
                name: "FK_Bills_Parties_PartyId",
                table: "Bills");

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceLine_Accounts_AccountId",
                table: "InvoiceLine");

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceLine_Items_ItemId",
                table: "InvoiceLine");

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceLine_TaxCodes_TaxCodeId",
                table: "InvoiceLine");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_JournalEntries_JournalEntryId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_LegalEntities_LegalEntityId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Parties_PartyId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Settlements_Accounts_BankAccountId",
                table: "Settlements");

            migrationBuilder.DropForeignKey(
                name: "FK_Settlements_JournalEntries_JournalEntryId",
                table: "Settlements");

            migrationBuilder.DropForeignKey(
                name: "FK_Settlements_LegalEntities_LegalEntityId",
                table: "Settlements");

            migrationBuilder.DropForeignKey(
                name: "FK_Settlements_Parties_PartyId",
                table: "Settlements");

            migrationBuilder.DropForeignKey(
                name: "FK_Settlements_PaymentInstruments_PaymentInstrumentId",
                table: "Settlements");

            migrationBuilder.DropIndex(
                name: "IX_Settlements_BankAccountId",
                table: "Settlements");

            migrationBuilder.DropIndex(
                name: "IX_Settlements_JournalEntryId",
                table: "Settlements");

            migrationBuilder.DropIndex(
                name: "IX_Settlements_LegalEntityId",
                table: "Settlements");

            migrationBuilder.DropIndex(
                name: "IX_Settlements_PartyId",
                table: "Settlements");

            migrationBuilder.DropIndex(
                name: "IX_Settlements_PaymentInstrumentId",
                table: "Settlements");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_JournalEntryId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_LegalEntityId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_PartyId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceLine_AccountId",
                table: "InvoiceLine");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceLine_ItemId",
                table: "InvoiceLine");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceLine_TaxCodeId",
                table: "InvoiceLine");

            migrationBuilder.DropIndex(
                name: "IX_Bills_JournalEntryId",
                table: "Bills");

            migrationBuilder.DropIndex(
                name: "IX_Bills_LegalEntityId",
                table: "Bills");

            migrationBuilder.DropIndex(
                name: "IX_Bills_PartyId",
                table: "Bills");

            migrationBuilder.DropIndex(
                name: "IX_BillLine_AccountId",
                table: "BillLine");

            migrationBuilder.DropIndex(
                name: "IX_BillLine_ItemId",
                table: "BillLine");

            migrationBuilder.DropIndex(
                name: "IX_BillLine_TaxCodeId",
                table: "BillLine");
        }
    }
}
