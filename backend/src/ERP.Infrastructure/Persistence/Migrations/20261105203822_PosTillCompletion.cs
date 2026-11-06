using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PosTillCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CardAccountId",
                table: "pos_Registers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExpenseAccountId",
                table: "pos_Registers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VarianceAccountId",
                table: "pos_Registers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InvoiceId",
                table: "pos_Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumber",
                table: "pos_Orders",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReturnedTotal",
                table: "pos_Orders",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReturnedAmount",
                table: "pos_OrderLines",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReturnedQuantity",
                table: "pos_OrderLines",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                table: "pos_DrawerTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PerformedByName",
                table: "pos_DrawerTransactions",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Barcode",
                table: "Items",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pos_Returns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OriginalOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginalNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RegisterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReturnDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SubTotal = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    TaxTotal = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CashierUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CashierName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CreditNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreditNoteNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
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
                    table.PrimaryKey("PK_pos_Returns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "pos_ReturnLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PosReturnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ItemCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    TaxRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Restock = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_pos_ReturnLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pos_ReturnLines_pos_Returns_PosReturnId",
                        column: x => x.PosReturnId,
                        principalTable: "pos_Returns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pos_ReturnRefunds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PosReturnId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_pos_ReturnRefunds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pos_ReturnRefunds_pos_Returns_PosReturnId",
                        column: x => x.PosReturnId,
                        principalTable: "pos_Returns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Items_OrgId_Barcode",
                table: "Items",
                columns: new[] { "OrgId", "Barcode" },
                unique: true,
                filter: "[Barcode] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_pos_ReturnLines_OrgId_PosReturnId",
                table: "pos_ReturnLines",
                columns: new[] { "OrgId", "PosReturnId" });

            migrationBuilder.CreateIndex(
                name: "IX_pos_ReturnLines_PosReturnId",
                table: "pos_ReturnLines",
                column: "PosReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_pos_ReturnRefunds_OrgId_PosReturnId",
                table: "pos_ReturnRefunds",
                columns: new[] { "OrgId", "PosReturnId" });

            migrationBuilder.CreateIndex(
                name: "IX_pos_ReturnRefunds_PosReturnId",
                table: "pos_ReturnRefunds",
                column: "PosReturnId");

            migrationBuilder.CreateIndex(
                name: "IX_pos_Returns_OrgId_Number",
                table: "pos_Returns",
                columns: new[] { "OrgId", "Number" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_pos_Returns_OrgId_OriginalOrderId",
                table: "pos_Returns",
                columns: new[] { "OrgId", "OriginalOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_pos_Returns_OrgId_SessionId",
                table: "pos_Returns",
                columns: new[] { "OrgId", "SessionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pos_ReturnLines");

            migrationBuilder.DropTable(
                name: "pos_ReturnRefunds");

            migrationBuilder.DropTable(
                name: "pos_Returns");

            migrationBuilder.DropIndex(
                name: "IX_Items_OrgId_Barcode",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "CardAccountId",
                table: "pos_Registers");

            migrationBuilder.DropColumn(
                name: "ExpenseAccountId",
                table: "pos_Registers");

            migrationBuilder.DropColumn(
                name: "VarianceAccountId",
                table: "pos_Registers");

            migrationBuilder.DropColumn(
                name: "InvoiceId",
                table: "pos_Orders");

            migrationBuilder.DropColumn(
                name: "InvoiceNumber",
                table: "pos_Orders");

            migrationBuilder.DropColumn(
                name: "ReturnedTotal",
                table: "pos_Orders");

            migrationBuilder.DropColumn(
                name: "ReturnedAmount",
                table: "pos_OrderLines");

            migrationBuilder.DropColumn(
                name: "ReturnedQuantity",
                table: "pos_OrderLines");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "pos_DrawerTransactions");

            migrationBuilder.DropColumn(
                name: "PerformedByName",
                table: "pos_DrawerTransactions");

            migrationBuilder.DropColumn(
                name: "Barcode",
                table: "Items");
        }
    }
}
