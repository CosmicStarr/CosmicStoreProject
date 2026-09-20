using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class OrderItemCancelFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('store.OrderItems', 'Name') IS NULL
                    ALTER TABLE [store].[OrderItems]
                    ADD [Name] NVARCHAR(MAX) NOT NULL
                    CONSTRAINT [DF_OrderItems_Name] DEFAULT ('');

                IF COL_LENGTH('store.OrderItems', 'Status') IS NULL
                    ALTER TABLE [store].[OrderItems]
                    ADD [Status] NVARCHAR(MAX) NOT NULL
                    CONSTRAINT [DF_OrderItems_Status] DEFAULT ('Ordered');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.default_constraints
                    WHERE name = 'DF_OrderItems_Name'
                )
                    ALTER TABLE [store].[OrderItems] DROP CONSTRAINT [DF_OrderItems_Name];

                IF EXISTS (
                    SELECT 1 FROM sys.default_constraints
                    WHERE name = 'DF_OrderItems_Status'
                )
                    ALTER TABLE [store].[OrderItems] DROP CONSTRAINT [DF_OrderItems_Status];

                IF COL_LENGTH('store.OrderItems', 'Name') IS NOT NULL
                    ALTER TABLE [store].[OrderItems] DROP COLUMN [Name];

                IF COL_LENGTH('store.OrderItems', 'Status') IS NOT NULL
                    ALTER TABLE [store].[OrderItems] DROP COLUMN [Status];
                """);
        }
    }
}
