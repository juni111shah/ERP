using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlatformConsole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPlatformAdmin",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsInternal",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PlatformActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    At = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Actor = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    OrgId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClientName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Detail = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformActivities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SignupMode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RequireEmailVerification = table.Column<bool>(type: "bit", nullable: false),
                    DefaultPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Currency = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    TaxPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    TaxLabel = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    InvoicePrefix = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    NextInvoiceNumber = table.Column<int>(type: "int", nullable: false),
                    InvoiceDueDays = table.Column<int>(type: "int", nullable: false),
                    RenewalLeadDays = table.Column<int>(type: "int", nullable: false),
                    GraceDays = table.Column<int>(type: "int", nullable: false),
                    SuspendAfterDays = table.Column<int>(type: "int", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    CompanyAddress = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CompanyTaxNumber = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    SupportEmail = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    TermsUrl = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    PaymentInstructions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsPublic = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsRecommended = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    MonthlyPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AnnualPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TrialDays = table.Column<int>(type: "int", nullable: false),
                    MaxUsers = table.Column<int>(type: "int", nullable: true),
                    MaxLegalEntities = table.Column<int>(type: "int", nullable: true),
                    ModuleKeys = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Features = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SignupRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    BusinessName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SignInKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Industry = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    CountryCode = table.Column<string>(type: "nchar(2)", fixedLength: true, maxLength: 2, nullable: false),
                    BaseCurrency = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    TimeZone = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cycle = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    VerificationTokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    VerificationExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EmailVerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RejectReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ProvisionedOrgId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedIp = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignupRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignupRequests_SubscriptionPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkspaceSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrgId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Cycle = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StartedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    TrialEndsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CurrentPeriodStart = table.Column<DateOnly>(type: "date", nullable: true),
                    CurrentPeriodEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    CustomPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IsComplimentary = table.Column<bool>(type: "bit", nullable: false),
                    LapsedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    SuspendedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SuspendedReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    SuspendedByFounder = table.Column<bool>(type: "bit", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    BillingEmail = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RequestedPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedCycle = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    RequestNote = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastReminderKey = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkspaceSubscriptions_Organizations_OrgId",
                        column: x => x.OrgId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkspaceSubscriptions_SubscriptionPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    OrgId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: true),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: true),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    IssuedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    DueOn = table.Column<DateOnly>(type: "date", nullable: false),
                    PaidOn = table.Column<DateOnly>(type: "date", nullable: true),
                    PaymentMethod = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    PaymentReference = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    VoidReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionInvoices_Organizations_OrgId",
                        column: x => x.OrgId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionInvoices_WorkspaceSubscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "WorkspaceSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformActivities_At",
                table: "PlatformActivities",
                column: "At");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformActivities_OrgId",
                table: "PlatformActivities",
                column: "OrgId");

            migrationBuilder.CreateIndex(
                name: "IX_SignupRequests_PlanId",
                table: "SignupRequests",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_SignupRequests_SignInKey",
                table: "SignupRequests",
                column: "SignInKey");

            migrationBuilder.CreateIndex(
                name: "IX_SignupRequests_Status",
                table: "SignupRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SignupRequests_VerificationTokenHash",
                table: "SignupRequests",
                column: "VerificationTokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionInvoices_Number",
                table: "SubscriptionInvoices",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionInvoices_OrgId_Status",
                table: "SubscriptionInvoices",
                columns: new[] { "OrgId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionInvoices_SubscriptionId",
                table: "SubscriptionInvoices",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlans_Code",
                table: "SubscriptionPlans",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceSubscriptions_OrgId",
                table: "WorkspaceSubscriptions",
                column: "OrgId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceSubscriptions_PlanId",
                table: "WorkspaceSubscriptions",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceSubscriptions_Status",
                table: "WorkspaceSubscriptions",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformActivities");

            migrationBuilder.DropTable(
                name: "PlatformSettings");

            migrationBuilder.DropTable(
                name: "SignupRequests");

            migrationBuilder.DropTable(
                name: "SubscriptionInvoices");

            migrationBuilder.DropTable(
                name: "WorkspaceSubscriptions");

            migrationBuilder.DropTable(
                name: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "IsPlatformAdmin",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsInternal",
                table: "Organizations");
        }
    }
}
