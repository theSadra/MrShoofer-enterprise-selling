using Application.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Migrations
{
  [DbContext(typeof(AppDbContext))]
  [Migration("20260919150000_AddAgencyInvoiceBuyerFields")]
  public partial class AddAgencyInvoiceBuyerFields : Migration
  {
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.AddColumn<string>(name: "Fax", table: "Agencies", type: "text", nullable: true);
      migrationBuilder.AddColumn<string>(name: "Province", table: "Agencies", type: "text", nullable: true);
      migrationBuilder.AddColumn<string>(name: "County", table: "Agencies", type: "text", nullable: true);
      migrationBuilder.AddColumn<string>(name: "City", table: "Agencies", type: "text", nullable: true);
      migrationBuilder.AddColumn<string>(name: "PostalCode", table: "Agencies", type: "text", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropColumn(name: "Fax", table: "Agencies");
      migrationBuilder.DropColumn(name: "Province", table: "Agencies");
      migrationBuilder.DropColumn(name: "County", table: "Agencies");
      migrationBuilder.DropColumn(name: "City", table: "Agencies");
      migrationBuilder.DropColumn(name: "PostalCode", table: "Agencies");
    }
  }
}
