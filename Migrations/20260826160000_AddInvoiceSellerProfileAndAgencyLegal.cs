using Application.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260826160000_AddInvoiceSellerProfileAndAgencyLegal")]
    public partial class AddInvoiceSellerProfileAndAgencyLegal : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "InvoiceSellerProfiles" (
                    "Id" serial PRIMARY KEY,
                    "CompanyName" character varying(200) NOT NULL DEFAULT 'گروه فناوری رهنگار',
                    "EconomicNo" character varying(32) NOT NULL DEFAULT '14015483891',
                    "RegistrationNo" character varying(32) NOT NULL DEFAULT '676497',
                    "NationalId" character varying(20) NOT NULL DEFAULT '14015483891',
                    "Phone" character varying(32) NOT NULL DEFAULT '021-28422243',
                    "Province" character varying(64) NOT NULL DEFAULT 'تهران',
                    "County" character varying(64) NOT NULL DEFAULT 'تهران',
                    "City" character varying(64) NOT NULL DEFAULT 'تهران',
                    "PostalCode" character varying(20) NOT NULL DEFAULT '1815767431',
                    "Address" character varying(500) NOT NULL DEFAULT '',
                    "InvoiceTitle" character varying(300) NOT NULL DEFAULT ''
                );

                INSERT INTO "InvoiceSellerProfiles" (
                    "CompanyName", "EconomicNo", "RegistrationNo", "NationalId", "Phone",
                    "Province", "County", "City", "PostalCode", "Address", "InvoiceTitle")
                SELECT
                    'گروه فناوری رهنگار', '14015483891', '676497', '14015483891', '021-28422243',
                    'تهران', 'تهران', 'تهران', '1815767431',
                    'تهران میرداماد میدان مادر خیابان شاه نظری کوچه ابن سینا پلاک ۲',
                    'صورتحساب فروش خدمات از مسترشوفر (گروه فناوری رهنگار)'
                WHERE NOT EXISTS (SELECT 1 FROM "InvoiceSellerProfiles");

                ALTER TABLE "Agencies" ADD COLUMN IF NOT EXISTS "EconomicNo" character varying(32) NULL;
                ALTER TABLE "Agencies" ADD COLUMN IF NOT EXISTS "RegistrationNo" character varying(32) NULL;
                ALTER TABLE "Agencies" ADD COLUMN IF NOT EXISTS "NationalId" character varying(20) NULL;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS "InvoiceSellerProfiles";
                ALTER TABLE "Agencies" DROP COLUMN IF EXISTS "EconomicNo";
                ALTER TABLE "Agencies" DROP COLUMN IF EXISTS "RegistrationNo";
                ALTER TABLE "Agencies" DROP COLUMN IF EXISTS "NationalId";
                """);
        }
    }
}
