using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChequesAndBank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BankAccountId",
                table: "PaymentInstruments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BounceReason",
                table: "PaymentInstruments",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ClearedOn",
                table: "PaymentInstruments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DepositedOn",
                table: "PaymentInstruments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PartyName",
                table: "PaymentInstruments",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReplacesInstrumentId",
                table: "PaymentInstruments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SettlementId",
                table: "PaymentInstruments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BankStatementLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    MatchedJournalLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_BankStatementLines", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_MatchedJournalLineId",
                table: "BankStatementLines",
                column: "MatchedJournalLineId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_OrgId_AccountId_Date",
                table: "BankStatementLines",
                columns: new[] { "OrgId", "AccountId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankStatementLines");

            migrationBuilder.DropColumn(
                name: "BankAccountId",
                table: "PaymentInstruments");

            migrationBuilder.DropColumn(
                name: "BounceReason",
                table: "PaymentInstruments");

            migrationBuilder.DropColumn(
                name: "ClearedOn",
                table: "PaymentInstruments");

            migrationBuilder.DropColumn(
                name: "DepositedOn",
                table: "PaymentInstruments");

            migrationBuilder.DropColumn(
                name: "PartyName",
                table: "PaymentInstruments");

            migrationBuilder.DropColumn(
                name: "ReplacesInstrumentId",
                table: "PaymentInstruments");

            migrationBuilder.DropColumn(
                name: "SettlementId",
                table: "PaymentInstruments");
        }
    }
}
