using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RealEstateModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "re_Properties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PropertyType = table.Column<int>(type: "int", nullable: false),
                    OwnershipType = table.Column<int>(type: "int", nullable: false),
                    OwnerPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Region = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Community = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PlotNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    YearBuilt = table.Column<int>(type: "int", nullable: true),
                    TotalUnits = table.Column<int>(type: "int", nullable: false),
                    IndicativeValue = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CustomFields = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_re_Properties", x => x.Id);
                    table.CheckConstraint("CK_re_Properties_CustomFields_IsJson", "[CustomFields] IS NULL OR ISJSON([CustomFields]) = 1");
                });

            migrationBuilder.CreateTable(
                name: "re_UnitTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DefaultUsage = table.Column<int>(type: "int", nullable: false),
                    DefaultBedrooms = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_re_UnitTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "re_Units",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UnitTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Usage = table.Column<int>(type: "int", nullable: false),
                    Floor = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Bedrooms = table.Column<int>(type: "int", nullable: true),
                    AreaSqft = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    AskingRent = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    VacantSince = table.Column<DateOnly>(type: "date", nullable: true),
                    ReservedUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    UnavailableReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CustomFields = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_re_Units", x => x.Id);
                    table.CheckConstraint("CK_re_Units_CustomFields_IsJson", "[CustomFields] IS NULL OR ISJSON([CustomFields]) = 1");
                    table.ForeignKey(
                        name: "FK_re_Units_re_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "re_Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_re_Units_re_UnitTypes_UnitTypeId",
                        column: x => x.UnitTypeId,
                        principalTable: "re_UnitTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "re_BrokerageDeals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    DealType = table.Column<int>(type: "int", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UnitReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ClientPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AgentPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExternalAgentName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TeamLeadPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DealDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Stage = table.Column<int>(type: "int", nullable: false),
                    DealValue = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    CommissionRatePercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    TaxCodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AgentSharePercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    TeamLeadSharePercent = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    GrossCommission = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    LostReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CustomFields = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_re_BrokerageDeals", x => x.Id);
                    table.CheckConstraint("CK_re_BrokerageDeals_CustomFields_IsJson", "[CustomFields] IS NULL OR ISJSON([CustomFields]) = 1");
                    table.ForeignKey(
                        name: "FK_re_BrokerageDeals_re_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "re_Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_re_BrokerageDeals_re_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "re_Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "re_Leases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    UnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LesseePartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AnnualRent = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    PaymentFrequency = table.Column<int>(type: "int", nullable: false),
                    SecurityDepositAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    TaxCodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ActivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActivatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TerminationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TerminationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RenewalOfLeaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GraceDays = table.Column<int>(type: "int", nullable: false),
                    NoticePeriodDays = table.Column<int>(type: "int", nullable: false),
                    HoldingOverFlag = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CustomFields = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_re_Leases", x => x.Id);
                    table.CheckConstraint("CK_re_Leases_Amounts", "[AnnualRent] > 0 AND [SecurityDepositAmount] >= 0");
                    table.CheckConstraint("CK_re_Leases_CustomFields_IsJson", "[CustomFields] IS NULL OR ISJSON([CustomFields]) = 1");
                    table.CheckConstraint("CK_re_Leases_Dates", "[EndDate] > [StartDate]");
                    table.CheckConstraint("CK_re_Leases_PaymentFrequency", "[PaymentFrequency] IN (1, 2, 4, 6, 12)");
                    table.ForeignKey(
                        name: "FK_re_Leases_re_Leases_RenewalOfLeaseId",
                        column: x => x.RenewalOfLeaseId,
                        principalTable: "re_Leases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_re_Leases_re_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "re_Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_re_Leases_re_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "re_Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "re_UnitRentRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    AnnualRent = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_re_UnitRentRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_re_UnitRentRates_re_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "re_Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "re_RentInstalments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    PeriodFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodTo = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_re_RentInstalments", x => x.Id);
                    table.CheckConstraint("CK_re_RentInstalments_Period", "[PeriodTo] >= [PeriodFrom] AND [Amount] >= 0");
                    table.ForeignKey(
                        name: "FK_re_RentInstalments_re_Leases_LeaseId",
                        column: x => x.LeaseId,
                        principalTable: "re_Leases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "re_WorkOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    PropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Issue = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReportedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ReportedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VendorPartyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Technician = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ScheduledFor = table.Column<DateOnly>(type: "date", nullable: true),
                    CompletedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    ActualCost = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CustomFields = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_re_WorkOrders", x => x.Id);
                    table.CheckConstraint("CK_re_WorkOrders_Costs", "[EstimatedCost] >= 0 AND ([ActualCost] IS NULL OR [ActualCost] >= 0)");
                    table.CheckConstraint("CK_re_WorkOrders_CustomFields_IsJson", "[CustomFields] IS NULL OR ISJSON([CustomFields]) = 1");
                    table.ForeignKey(
                        name: "FK_re_WorkOrders_re_Leases_LeaseId",
                        column: x => x.LeaseId,
                        principalTable: "re_Leases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_re_WorkOrders_re_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "re_Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_re_WorkOrders_re_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "re_Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_re_BrokerageDeals_OrgId_AgentPartyId",
                table: "re_BrokerageDeals",
                columns: new[] { "OrgId", "AgentPartyId" });

            migrationBuilder.CreateIndex(
                name: "IX_re_BrokerageDeals_OrgId_LegalEntityId_Number",
                table: "re_BrokerageDeals",
                columns: new[] { "OrgId", "LegalEntityId", "Number" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Number] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_re_BrokerageDeals_OrgId_LegalEntityId_Stage_DealDate",
                table: "re_BrokerageDeals",
                columns: new[] { "OrgId", "LegalEntityId", "Stage", "DealDate" });

            migrationBuilder.CreateIndex(
                name: "IX_re_BrokerageDeals_PropertyId",
                table: "re_BrokerageDeals",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_re_BrokerageDeals_UnitId",
                table: "re_BrokerageDeals",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_re_Leases_OrgId_LegalEntityId_Number",
                table: "re_Leases",
                columns: new[] { "OrgId", "LegalEntityId", "Number" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Number] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_re_Leases_OrgId_LegalEntityId_Status_EndDate",
                table: "re_Leases",
                columns: new[] { "OrgId", "LegalEntityId", "Status", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_re_Leases_OrgId_LesseePartyId",
                table: "re_Leases",
                columns: new[] { "OrgId", "LesseePartyId" });

            migrationBuilder.CreateIndex(
                name: "IX_re_Leases_OrgId_PropertyId",
                table: "re_Leases",
                columns: new[] { "OrgId", "PropertyId" });

            migrationBuilder.CreateIndex(
                name: "IX_re_Leases_PropertyId",
                table: "re_Leases",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_re_Leases_RenewalOfLeaseId",
                table: "re_Leases",
                column: "RenewalOfLeaseId");

            migrationBuilder.CreateIndex(
                name: "IX_re_Leases_UnitId",
                table: "re_Leases",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "UX_re_Leases_OneOccupyingPerUnit",
                table: "re_Leases",
                columns: new[] { "OrgId", "UnitId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Status] IN (1, 5)");

            migrationBuilder.CreateIndex(
                name: "UX_re_Leases_OneRenewal",
                table: "re_Leases",
                columns: new[] { "OrgId", "RenewalOfLeaseId" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [RenewalOfLeaseId] IS NOT NULL AND [Status] <> 6");

            migrationBuilder.CreateIndex(
                name: "IX_re_Properties_OrgId_Code",
                table: "re_Properties",
                columns: new[] { "OrgId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_re_Properties_OrgId_LegalEntityId_IsActive",
                table: "re_Properties",
                columns: new[] { "OrgId", "LegalEntityId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_re_Properties_OrgId_OwnerPartyId",
                table: "re_Properties",
                columns: new[] { "OrgId", "OwnerPartyId" });

            migrationBuilder.CreateIndex(
                name: "IX_re_RentInstalments_LeaseId",
                table: "re_RentInstalments",
                column: "LeaseId");

            migrationBuilder.CreateIndex(
                name: "IX_re_RentInstalments_OrgId_DueDate",
                table: "re_RentInstalments",
                columns: new[] { "OrgId", "DueDate" },
                filter: "[IsDeleted] = 0 AND [Status] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_re_RentInstalments_OrgId_LeaseId_Sequence",
                table: "re_RentInstalments",
                columns: new[] { "OrgId", "LeaseId", "Sequence" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_re_UnitRentRates_OrgId_UnitId_EffectiveFrom",
                table: "re_UnitRentRates",
                columns: new[] { "OrgId", "UnitId", "EffectiveFrom" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_re_UnitRentRates_UnitId",
                table: "re_UnitRentRates",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_re_Units_OrgId_LegalEntityId_Status_PropertyId",
                table: "re_Units",
                columns: new[] { "OrgId", "LegalEntityId", "Status", "PropertyId" });

            migrationBuilder.CreateIndex(
                name: "IX_re_Units_OrgId_PropertyId_UnitNumber",
                table: "re_Units",
                columns: new[] { "OrgId", "PropertyId", "UnitNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_re_Units_PropertyId",
                table: "re_Units",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_re_Units_UnitTypeId",
                table: "re_Units",
                column: "UnitTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_re_UnitTypes_OrgId_Code",
                table: "re_UnitTypes",
                columns: new[] { "OrgId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_re_WorkOrders_LeaseId",
                table: "re_WorkOrders",
                column: "LeaseId");

            migrationBuilder.CreateIndex(
                name: "IX_re_WorkOrders_OrgId_LegalEntityId_Number",
                table: "re_WorkOrders",
                columns: new[] { "OrgId", "LegalEntityId", "Number" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [Number] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_re_WorkOrders_OrgId_LegalEntityId_Status_Priority",
                table: "re_WorkOrders",
                columns: new[] { "OrgId", "LegalEntityId", "Status", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_re_WorkOrders_OrgId_PropertyId_ReportedOn",
                table: "re_WorkOrders",
                columns: new[] { "OrgId", "PropertyId", "ReportedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_re_WorkOrders_OrgId_UnitId",
                table: "re_WorkOrders",
                columns: new[] { "OrgId", "UnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_re_WorkOrders_PropertyId",
                table: "re_WorkOrders",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_re_WorkOrders_UnitId",
                table: "re_WorkOrders",
                column: "UnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "re_BrokerageDeals");

            migrationBuilder.DropTable(
                name: "re_RentInstalments");

            migrationBuilder.DropTable(
                name: "re_UnitRentRates");

            migrationBuilder.DropTable(
                name: "re_WorkOrders");

            migrationBuilder.DropTable(
                name: "re_Leases");

            migrationBuilder.DropTable(
                name: "re_Units");

            migrationBuilder.DropTable(
                name: "re_Properties");

            migrationBuilder.DropTable(
                name: "re_UnitTypes");
        }
    }
}
