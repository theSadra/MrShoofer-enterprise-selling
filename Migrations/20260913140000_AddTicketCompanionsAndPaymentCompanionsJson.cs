using Application.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260913140000_AddTicketCompanionsAndPaymentCompanionsJson")]
    public partial class AddTicketCompanionsAndPaymentCompanionsJson : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanionsJson",
                table: "ZarinpalTicketPayments",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TicketCompanions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TicketId = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Firstname = table.Column<string>(type: "text", nullable: false),
                    Lastname = table.Column<string>(type: "text", nullable: false),
                    Gender = table.Column<string>(type: "text", nullable: false),
                    NaCode = table.Column<string>(type: "text", nullable: false),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    AgencyEmployeeId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketCompanions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketCompanions_AgencyEmployees_AgencyEmployeeId",
                        column: x => x.AgencyEmployeeId,
                        principalTable: "AgencyEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TicketCompanions_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketCompanions_AgencyEmployeeId",
                table: "TicketCompanions",
                column: "AgencyEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketCompanions_TicketId",
                table: "TicketCompanions",
                column: "TicketId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TicketCompanions");
            migrationBuilder.DropColumn(name: "CompanionsJson", table: "ZarinpalTicketPayments");
        }
    }
}
