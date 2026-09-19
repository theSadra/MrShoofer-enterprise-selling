using Application.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260826120000_AddOfficialInvoices")]
    public partial class AddOfficialInvoices : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "InvoiceSerialConfigs" (
                    "Id" serial PRIMARY KEY,
                    "Pattern" character varying(64) NOT NULL DEFAULT '{n}-{prefix}',
                    "Prefix" character varying(32) NOT NULL DEFAULT '2209',
                    "NextSequence" integer NOT NULL DEFAULT 1
                );

                INSERT INTO "InvoiceSerialConfigs" ("Pattern", "Prefix", "NextSequence")
                SELECT '{n}-{prefix}', '2209', 1
                WHERE NOT EXISTS (SELECT 1 FROM "InvoiceSerialConfigs");

                CREATE TABLE IF NOT EXISTS "OfficialInvoices" (
                    "Id" serial PRIMARY KEY,
                    "SerialNumber" character varying(64) NOT NULL,
                    "InvoiceDate" timestamp without time zone NOT NULL,
                    "CreatedAt" timestamp without time zone NOT NULL,
                    "CreatedBy" character varying(256) NULL,
                    "AgencyId" integer NOT NULL,
                    "TicketId" integer NULL,
                    "TicketCode" character varying(32) NULL,
                    "BuyerName" character varying(200) NULL,
                    "BuyerEconomicNo" character varying(32) NULL,
                    "BuyerRegistrationNo" character varying(32) NULL,
                    "BuyerProvince" character varying(64) NULL,
                    "BuyerCounty" character varying(64) NULL,
                    "BuyerCity" character varying(64) NULL,
                    "BuyerPostalCode" character varying(20) NULL,
                    "BuyerAddress" character varying(500) NULL,
                    "BuyerNationalId" character varying(20) NULL,
                    "BuyerPhone" character varying(32) NULL,
                    "BuyerFax" character varying(32) NULL,
                    "LinesJson" text NOT NULL DEFAULT '[]',
                    "TotalRials" bigint NOT NULL DEFAULT 0,
                    "DiscountRials" bigint NOT NULL DEFAULT 0,
                    "VatRials" bigint NOT NULL DEFAULT 0,
                    "PayableRials" bigint NOT NULL DEFAULT 0,
                    "PaymentIsCash" boolean NOT NULL DEFAULT TRUE,
                    "Notes" character varying(500) NULL,
                    CONSTRAINT "FK_OfficialInvoices_Agencies_AgencyId"
                        FOREIGN KEY ("AgencyId") REFERENCES "Agencies" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_OfficialInvoices_Tickets_TicketId"
                        FOREIGN KEY ("TicketId") REFERENCES "Tickets" ("Id") ON DELETE SET NULL
                );

                CREATE UNIQUE INDEX IF NOT EXISTS "IX_OfficialInvoices_SerialNumber"
                    ON "OfficialInvoices" ("SerialNumber");
                CREATE INDEX IF NOT EXISTS "IX_OfficialInvoices_AgencyId"
                    ON "OfficialInvoices" ("AgencyId");
                CREATE INDEX IF NOT EXISTS "IX_OfficialInvoices_TicketId"
                    ON "OfficialInvoices" ("TicketId");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS "OfficialInvoices";
                DROP TABLE IF EXISTS "InvoiceSerialConfigs";
                """);
        }
    }
}
