using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinanceVoidAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "Settlements",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAt",
                table: "Settlements",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VoidedBy",
                table: "Settlements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "Invoices",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAt",
                table: "Invoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VoidedBy",
                table: "Invoices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "Bills",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAt",
                table: "Bills",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VoidedBy",
                table: "Bills",
                type: "uniqueidentifier",
                nullable: true);

            // Backfill voided rows before the CHECKs: when = last update (or creation), who = last updater, why = the "Voided" audit event's reason when there is one.
            migrationBuilder.Sql(@"
UPDATE d SET d.[VoidedAt] = COALESCE(d.[UpdatedAt], d.[CreatedAt]), d.[VoidedBy] = d.[UpdatedBy],
    d.[VoidReason] = (SELECT TOP (1) LEFT(CASE WHEN ISJSON(ae.[After]) = 1 THEN JSON_VALUE(ae.[After], '$.reason') END, 500)
                      FROM [AuditEvents] ae
                      WHERE ae.[Action] = N'Voided' AND ae.[RecordType] = N'Invoice' AND ae.[RecordId] = CONVERT(nvarchar(36), d.[Id])
                      ORDER BY ae.[OccurredAt] DESC)
FROM [Invoices] d
WHERE d.[Status] = N'Voided' AND d.[VoidedAt] IS NULL;

UPDATE d SET d.[VoidedAt] = COALESCE(d.[UpdatedAt], d.[CreatedAt]), d.[VoidedBy] = d.[UpdatedBy],
    d.[VoidReason] = (SELECT TOP (1) LEFT(CASE WHEN ISJSON(ae.[After]) = 1 THEN JSON_VALUE(ae.[After], '$.reason') END, 500)
                      FROM [AuditEvents] ae
                      WHERE ae.[Action] = N'Voided' AND ae.[RecordType] = N'Bill' AND ae.[RecordId] = CONVERT(nvarchar(36), d.[Id])
                      ORDER BY ae.[OccurredAt] DESC)
FROM [Bills] d
WHERE d.[Status] = N'Voided' AND d.[VoidedAt] IS NULL;

UPDATE d SET d.[VoidedAt] = COALESCE(d.[UpdatedAt], d.[CreatedAt]), d.[VoidedBy] = d.[UpdatedBy],
    d.[VoidReason] = (SELECT TOP (1) LEFT(CASE WHEN ISJSON(ae.[After]) = 1 THEN JSON_VALUE(ae.[After], '$.reason') END, 500)
                      FROM [AuditEvents] ae
                      WHERE ae.[Action] = N'Voided' AND ae.[RecordType] = N'Settlement' AND ae.[RecordId] = CONVERT(nvarchar(36), d.[Id])
                      ORDER BY ae.[OccurredAt] DESC)
FROM [Settlements] d
WHERE d.[Status] = N'Voided' AND d.[VoidedAt] IS NULL;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Settlements_Voided",
                table: "Settlements",
                sql: "([Status] = N'Voided' AND [VoidedAt] IS NOT NULL) OR ([Status] <> N'Voided' AND [VoidedAt] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_Voided",
                table: "Invoices",
                sql: "([Status] = N'Voided' AND [VoidedAt] IS NOT NULL) OR ([Status] <> N'Voided' AND [VoidedAt] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bills_Voided",
                table: "Bills",
                sql: "([Status] = N'Voided' AND [VoidedAt] IS NOT NULL) OR ([Status] <> N'Voided' AND [VoidedAt] IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Settlements_Voided",
                table: "Settlements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_Voided",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Bills_Voided",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "VoidedBy",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VoidedBy",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "VoidedBy",
                table: "Bills");
        }
    }
}
