using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OwnerCostBasis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BillId",
                table: "re_WorkOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChargeableTo",
                table: "re_WorkOrders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OwnerSharePercent",
                table: "re_WorkOrders",
                type: "decimal(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                defaultValue: 100m);

            migrationBuilder.CreateIndex(
                name: "IX_re_WorkOrders_OrgId_BillId",
                table: "re_WorkOrders",
                columns: new[] { "OrgId", "BillId" },
                filter: "[BillId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_re_WorkOrders_OrgId_BillId",
                table: "re_WorkOrders");

            migrationBuilder.DropColumn(
                name: "BillId",
                table: "re_WorkOrders");

            migrationBuilder.DropColumn(
                name: "ChargeableTo",
                table: "re_WorkOrders");

            migrationBuilder.DropColumn(
                name: "OwnerSharePercent",
                table: "re_WorkOrders");
        }
    }
}
