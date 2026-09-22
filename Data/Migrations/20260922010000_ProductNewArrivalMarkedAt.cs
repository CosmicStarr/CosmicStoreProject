using Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    [DbContext(typeof(ApplicationDbStoreContext))]
    [Migration("20260922010000_ProductNewArrivalMarkedAt")]
    public partial class ProductNewArrivalMarkedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('store.GetProducts', 'NewArrivalMarkedAt') IS NULL
                    ALTER TABLE [store].[GetProducts] ADD [NewArrivalMarkedAt] DATETIME2 NULL;
                """);

            // Separate batch so SQL Server does not validate the column before ALTER runs.
            migrationBuilder.Sql("""
                UPDATE [store].[GetProducts]
                SET [NewArrivalMarkedAt] = SYSUTCDATETIME()
                WHERE [IsNewArrival] = 1 AND [NewArrivalMarkedAt] IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('store.GetProducts', 'NewArrivalMarkedAt') IS NOT NULL
                    ALTER TABLE [store].[GetProducts] DROP COLUMN [NewArrivalMarkedAt];
                """);
        }
    }
}
