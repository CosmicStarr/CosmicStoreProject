using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class OrderZipCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('store.Orders', 'ZipCode') IS NULL
                    ALTER TABLE [store].[Orders]
                    ADD [ZipCode] NVARCHAR(MAX) NOT NULL
                    CONSTRAINT [DF_Orders_ZipCode] DEFAULT ('');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.default_constraints
                    WHERE name = 'DF_Orders_ZipCode'
                )
                    ALTER TABLE [store].[Orders] DROP CONSTRAINT [DF_Orders_ZipCode];

                IF COL_LENGTH('store.Orders', 'ZipCode') IS NOT NULL
                    ALTER TABLE [store].[Orders] DROP COLUMN [ZipCode];
                """);
        }
    }
}
