using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BillBusinessUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BusinessUnitId",
                table: "Bills",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_BusinessUnitId",
                table: "Invoices",
                column: "BusinessUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_BusinessUnitId",
                table: "Bills",
                column: "BusinessUnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bills_BusinessUnits_BusinessUnitId",
                table: "Bills",
                column: "BusinessUnitId",
                principalTable: "BusinessUnits",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_BusinessUnits_BusinessUnitId",
                table: "Invoices",
                column: "BusinessUnitId",
                principalTable: "BusinessUnits",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bills_BusinessUnits_BusinessUnitId",
                table: "Bills");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_BusinessUnits_BusinessUnitId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_BusinessUnitId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Bills_BusinessUnitId",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "BusinessUnitId",
                table: "Bills");
        }
    }
}
