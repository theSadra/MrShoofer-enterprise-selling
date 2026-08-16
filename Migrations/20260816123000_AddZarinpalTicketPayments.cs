using System;
using Application.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Application.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260816123000_AddZarinpalTicketPayments")]
    public partial class AddZarinpalTicketPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ZarinpalTicketPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Authority = table.Column<string>(type: "text", nullable: false),
                    AmountToman = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RefId = table.Column<long>(type: "bigint", nullable: true),
                    CardPan = table.Column<string>(type: "text", nullable: true),
                    TripCode = table.Column<string>(type: "text", nullable: false),
                    Firstname = table.Column<string>(type: "text", nullable: false),
                    Lastname = table.Column<string>(type: "text", nullable: false),
                    Numberphone = table.Column<string>(type: "text", nullable: false),
                    Nacode = table.Column<string>(type: "text", nullable: false),
                    Gender = table.Column<string>(type: "text", nullable: false),
                    TicketId = table.Column<int>(type: "integer", nullable: true),
                    AgencyId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZarinpalTicketPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZarinpalTicketPayments_Agencies_AgencyId",
                        column: x => x.AgencyId,
                        principalTable: "Agencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ZarinpalTicketPayments_AgencyId",
                table: "ZarinpalTicketPayments",
                column: "AgencyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ZarinpalTicketPayments");
        }
    }
}
