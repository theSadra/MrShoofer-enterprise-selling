using Application.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Migrations
{
  [DbContext(typeof(AppDbContext))]
  [Migration("20260919140000_AddAgencyPanelTypeAndHeadOfPassengers")]
  public partial class AddAgencyPanelTypeAndHeadOfPassengers : Migration
  {
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.AddColumn<int>(
        name: "PanelType",
        table: "Agencies",
        type: "integer",
        nullable: false,
        defaultValue: 0);

      migrationBuilder.AddColumn<string>(
        name: "HeadOfPassengers",
        table: "Tickets",
        type: "text",
        nullable: true);

      migrationBuilder.AddColumn<string>(
        name: "HeadOfPassengers",
        table: "ZarinpalTicketPayments",
        type: "text",
        nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropColumn(name: "PanelType", table: "Agencies");
      migrationBuilder.DropColumn(name: "HeadOfPassengers", table: "Tickets");
      migrationBuilder.DropColumn(name: "HeadOfPassengers", table: "ZarinpalTicketPayments");
    }
  }
}
