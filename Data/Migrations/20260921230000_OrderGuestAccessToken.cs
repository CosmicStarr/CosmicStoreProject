using Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    [DbContext(typeof(ApplicationDbStoreContext))]
    [Migration("20260921230000_OrderGuestAccessToken")]
    public partial class OrderGuestAccessToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('store.Orders', 'GuestAccessTokenHash') IS NULL
                    ALTER TABLE [store].[Orders] ADD [GuestAccessTokenHash] NVARCHAR(64) NULL;

                IF COL_LENGTH('store.Orders', 'GuestAccessTokenExpiresAt') IS NULL
                    ALTER TABLE [store].[Orders] ADD [GuestAccessTokenExpiresAt] DATETIME2 NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('store.Orders', 'GuestAccessTokenHash') IS NOT NULL
                    ALTER TABLE [store].[Orders] DROP COLUMN [GuestAccessTokenHash];

                IF COL_LENGTH('store.Orders', 'GuestAccessTokenExpiresAt') IS NOT NULL
                    ALTER TABLE [store].[Orders] DROP COLUMN [GuestAccessTokenExpiresAt];
                """);
        }
    }
}
