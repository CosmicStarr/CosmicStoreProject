using Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    [DbContext(typeof(ApplicationDbStoreContext))]
    [Migration("20260921250000_AcceptedTerms")]
    public partial class AcceptedTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('store.Orders', 'AcceptedTermsAt') IS NULL
                    ALTER TABLE [store].[Orders] ADD [AcceptedTermsAt] DATETIME2 NULL;

                IF COL_LENGTH('store.Orders', 'AcceptedTermsVersion') IS NULL
                    ALTER TABLE [store].[Orders] ADD [AcceptedTermsVersion] NVARCHAR(32) NULL;
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('store.AspNetUsers', 'AcceptedTermsAt') IS NULL
                    ALTER TABLE [store].[AspNetUsers] ADD [AcceptedTermsAt] DATETIME2 NULL;

                IF COL_LENGTH('store.AspNetUsers', 'AcceptedTermsVersion') IS NULL
                    ALTER TABLE [store].[AspNetUsers] ADD [AcceptedTermsVersion] NVARCHAR(32) NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('store.Orders', 'AcceptedTermsAt') IS NOT NULL
                    ALTER TABLE [store].[Orders] DROP COLUMN [AcceptedTermsAt];

                IF COL_LENGTH('store.Orders', 'AcceptedTermsVersion') IS NOT NULL
                    ALTER TABLE [store].[Orders] DROP COLUMN [AcceptedTermsVersion];
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('store.AspNetUsers', 'AcceptedTermsAt') IS NOT NULL
                    ALTER TABLE [store].[AspNetUsers] DROP COLUMN [AcceptedTermsAt];

                IF COL_LENGTH('store.AspNetUsers', 'AcceptedTermsVersion') IS NOT NULL
                    ALTER TABLE [store].[AspNetUsers] DROP COLUMN [AcceptedTermsVersion];
                """);
        }
    }
}
