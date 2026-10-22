using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Acquisitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "re_Acquisitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SellerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PurchaseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ContractReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    DownPayment = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    TransferFees = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    OtherCosts = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    PayFromAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CurrencyCode = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_re_Acquisitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_re_Acquisitions_re_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "re_Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "re_FundingSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcquisitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Lender = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Principal = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    AnnualRatePercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    TermMonths = table.Column<int>(type: "int", nullable: false),
                    FirstDueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    MonthlyPayment = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
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
                    table.PrimaryKey("PK_re_FundingSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_re_FundingSources_re_Acquisitions_AcquisitionId",
                        column: x => x.AcquisitionId,
                        principalTable: "re_Acquisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "re_FundingInstalments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FundingSourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Principal = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Interest = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PaidOn = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_re_FundingInstalments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_re_FundingInstalments_re_FundingSources_FundingSourceId",
                        column: x => x.FundingSourceId,
                        principalTable: "re_FundingSources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_re_Acquisitions_OrgId_LegalEntityId_Number",
                table: "re_Acquisitions",
                columns: new[] { "OrgId", "LegalEntityId", "Number" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Number] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_re_Acquisitions_OrgId_PropertyId",
                table: "re_Acquisitions",
                columns: new[] { "OrgId", "PropertyId" });

            migrationBuilder.CreateIndex(
                name: "IX_re_Acquisitions_PropertyId",
                table: "re_Acquisitions",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_re_FundingInstalments_FundingSourceId",
                table: "re_FundingInstalments",
                column: "FundingSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_re_FundingInstalments_OrgId_FundingSourceId_Sequence",
                table: "re_FundingInstalments",
                columns: new[] { "OrgId", "FundingSourceId", "Sequence" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_re_FundingInstalments_OrgId_Status_DueDate",
                table: "re_FundingInstalments",
                columns: new[] { "OrgId", "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_re_FundingSources_AcquisitionId",
                table: "re_FundingSources",
                column: "AcquisitionId");

            migrationBuilder.CreateIndex(
                name: "IX_re_FundingSources_OrgId_AcquisitionId",
                table: "re_FundingSources",
                columns: new[] { "OrgId", "AcquisitionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "re_FundingInstalments");

            migrationBuilder.DropTable(
                name: "re_FundingSources");

            migrationBuilder.DropTable(
                name: "re_Acquisitions");
        }
    }
}
