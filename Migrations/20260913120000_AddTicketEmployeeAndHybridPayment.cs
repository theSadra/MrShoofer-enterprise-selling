using Application.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260913120000_AddTicketEmployeeAndHybridPayment")]
    public partial class AddTicketEmployeeAndHybridPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AgencyEmployeeId",
                table: "Tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AgencyEmployeeId",
                table: "ZarinpalTicketPayments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WalletAppliedToman",
                table: "ZarinpalTicketPayments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TicketPriceToman",
                table: "ZarinpalTicketPayments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_AgencyEmployeeId",
                table: "Tickets",
                column: "AgencyEmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_AgencyEmployees_AgencyEmployeeId",
                table: "Tickets",
                column: "AgencyEmployeeId",
                principalTable: "AgencyEmployees",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_AgencyEmployees_AgencyEmployeeId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_AgencyEmployeeId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "AgencyEmployeeId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "AgencyEmployeeId",
                table: "ZarinpalTicketPayments");

            migrationBuilder.DropColumn(
                name: "WalletAppliedToman",
                table: "ZarinpalTicketPayments");

            migrationBuilder.DropColumn(
                name: "TicketPriceToman",
                table: "ZarinpalTicketPayments");
        }
    }
}
