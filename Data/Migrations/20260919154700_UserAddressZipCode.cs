using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class UserAddressZipCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('store.UserAddresses', 'ZipCode') IS NULL
                    ALTER TABLE [store].[UserAddresses]
                    ADD [ZipCode] NVARCHAR(MAX) NOT NULL
                    CONSTRAINT [DF_UserAddresses_ZipCode] DEFAULT ('');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.default_constraints
                    WHERE name = 'DF_UserAddresses_ZipCode'
                )
                    ALTER TABLE [store].[UserAddresses] DROP CONSTRAINT [DF_UserAddresses_ZipCode];

                IF COL_LENGTH('store.UserAddresses', 'ZipCode') IS NOT NULL
                    ALTER TABLE [store].[UserAddresses] DROP COLUMN [ZipCode];
                """);
        }
    }
}
