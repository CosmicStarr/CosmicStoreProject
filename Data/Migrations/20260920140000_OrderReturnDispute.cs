using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class OrderReturnDispute : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('store.Orders', 'ReturnTrackingNumber') IS NULL
                    ALTER TABLE [store].[Orders]
                    ADD [ReturnTrackingNumber] NVARCHAR(MAX) NULL;

                IF COL_LENGTH('store.Orders', 'CjDisputeId') IS NULL
                    ALTER TABLE [store].[Orders]
                    ADD [CjDisputeId] NVARCHAR(MAX) NULL;

                IF COL_LENGTH('store.Orders', 'CjDisputeStatus') IS NULL
                    ALTER TABLE [store].[Orders]
                    ADD [CjDisputeStatus] NVARCHAR(MAX) NULL;

                IF COL_LENGTH('store.Orders', 'ReturnReceived') IS NULL
                    ALTER TABLE [store].[Orders]
                    ADD [ReturnReceived] BIT NOT NULL
                    CONSTRAINT [DF_Orders_ReturnReceived] DEFAULT (0);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.default_constraints
                    WHERE name = 'DF_Orders_ReturnReceived'
                )
                    ALTER TABLE [store].[Orders] DROP CONSTRAINT [DF_Orders_ReturnReceived];

                IF COL_LENGTH('store.Orders', 'ReturnTrackingNumber') IS NOT NULL
                    ALTER TABLE [store].[Orders] DROP COLUMN [ReturnTrackingNumber];

                IF COL_LENGTH('store.Orders', 'CjDisputeId') IS NOT NULL
                    ALTER TABLE [store].[Orders] DROP COLUMN [CjDisputeId];

                IF COL_LENGTH('store.Orders', 'CjDisputeStatus') IS NOT NULL
                    ALTER TABLE [store].[Orders] DROP COLUMN [CjDisputeStatus];

                IF COL_LENGTH('store.Orders', 'ReturnReceived') IS NOT NULL
                    ALTER TABLE [store].[Orders] DROP COLUMN [ReturnReceived];
                """);
        }
    }
}
