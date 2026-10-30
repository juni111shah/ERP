using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChequeBounceFollowUp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "BouncedOn",
                table: "PaymentInstruments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FollowUpNote",
                table: "PaymentInstruments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "FollowUpOn",
                table: "PaymentInstruments",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BouncedOn",
                table: "PaymentInstruments");

            migrationBuilder.DropColumn(
                name: "FollowUpNote",
                table: "PaymentInstruments");

            migrationBuilder.DropColumn(
                name: "FollowUpOn",
                table: "PaymentInstruments");
        }
    }
}
