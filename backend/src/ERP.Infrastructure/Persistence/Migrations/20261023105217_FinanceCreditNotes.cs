using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinanceCreditNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreditNoteForId",
                table: "Invoices",
                type: "uniqueidentifier",
                nullable: true);

            // Backfill: credit notes raised from an invoice (SourceType 'invoice') and real-estate termination credits
            // (SourceId = the rent instalment, whose InvoiceId is the credited invoice).
            migrationBuilder.Sql(@"
UPDATE cn SET cn.[CreditNoteForId] = src.[Id]
FROM [Invoices] cn
JOIN [Invoices] src ON src.[Id] = cn.[SourceId]
WHERE cn.[IsCreditNote] = 1 AND cn.[SourceType] = N'invoice' AND cn.[CreditNoteForId] IS NULL AND src.[Id] <> cn.[Id];

UPDATE cn SET cn.[CreditNoteForId] = src.[Id]
FROM [Invoices] cn
JOIN [re_RentInstalments] ri ON ri.[Id] = cn.[SourceId]
JOIN [Invoices] src ON src.[Id] = ri.[InvoiceId]
WHERE cn.[IsCreditNote] = 1 AND cn.[SourceType] = N'realestate.termination' AND cn.[CreditNoteForId] IS NULL AND src.[Id] <> cn.[Id];");

            migrationBuilder.CreateTable(
                name: "CreditApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreditNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    AppliedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_CreditApplications", x => x.Id);
                    table.CheckConstraint("CK_CreditApplications_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_CreditApplications_Distinct", "[CreditNoteId] <> [InvoiceId]");
                    table.ForeignKey(
                        name: "FK_CreditApplications_Invoices_CreditNoteId",
                        column: x => x.CreditNoteId,
                        principalTable: "Invoices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CreditApplications_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CreditNoteForId",
                table: "Invoices",
                column: "CreditNoteForId",
                filter: "[CreditNoteForId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_CreditNoteFor",
                table: "Invoices",
                sql: "[CreditNoteForId] IS NULL OR ([IsCreditNote] = 1 AND [CreditNoteForId] <> [Id])");

            migrationBuilder.CreateIndex(
                name: "IX_CreditApplications_CreditNoteId",
                table: "CreditApplications",
                column: "CreditNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditApplications_InvoiceId",
                table: "CreditApplications",
                column: "InvoiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Invoices_CreditNoteForId",
                table: "Invoices",
                column: "CreditNoteForId",
                principalTable: "Invoices",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Invoices_CreditNoteForId",
                table: "Invoices");

            migrationBuilder.DropTable(
                name: "CreditApplications");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_CreditNoteForId",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_CreditNoteFor",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CreditNoteForId",
                table: "Invoices");
        }
    }
}
