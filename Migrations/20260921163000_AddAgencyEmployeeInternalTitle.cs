using Application.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Migrations
{
  [DbContext(typeof(AppDbContext))]
  [Migration("20260921163000_AddAgencyEmployeeInternalTitle")]
  public partial class AddAgencyEmployeeInternalTitle : Migration
  {
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.AddColumn<string>(
        name: "InternalTitle",
        table: "AgencyEmployees",
        type: "character varying(120)",
        maxLength: 120,
        nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropColumn(name: "InternalTitle", table: "AgencyEmployees");
    }
  }
}
