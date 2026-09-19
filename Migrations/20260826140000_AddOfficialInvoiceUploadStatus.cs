using Application.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Application.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260826140000_AddOfficialInvoiceUploadStatus")]
    public partial class AddOfficialInvoiceUploadStatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "OfficialInvoices"
                    ADD COLUMN IF NOT EXISTS "UploadStatus" smallint NOT NULL DEFAULT 0;
                ALTER TABLE "OfficialInvoices"
                    ADD COLUMN IF NOT EXISTS "UploadedAt" timestamp without time zone NULL;
                ALTER TABLE "OfficialInvoices"
                    ADD COLUMN IF NOT EXISTS "UploadedBy" character varying(256) NULL;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "OfficialInvoices" DROP COLUMN IF EXISTS "UploadedBy";
                ALTER TABLE "OfficialInvoices" DROP COLUMN IF EXISTS "UploadedAt";
                ALTER TABLE "OfficialInvoices" DROP COLUMN IF EXISTS "UploadStatus";
                """);
        }
    }
}
