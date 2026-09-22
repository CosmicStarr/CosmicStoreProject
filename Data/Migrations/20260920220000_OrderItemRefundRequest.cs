using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class OrderItemRefundRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('store.OrderItems', 'RefundRequestedAt') IS NULL
                    ALTER TABLE [store].[OrderItems] ADD [RefundRequestedAt] DATETIME2 NULL;

                IF COL_LENGTH('store.OrderItems', 'RefundRequestReason') IS NULL
                    ALTER TABLE [store].[OrderItems] ADD [RefundRequestReason] NVARCHAR(MAX) NULL;

                IF COL_LENGTH('store.OrderItems', 'ReturnTrackingNumber') IS NULL
                    ALTER TABLE [store].[OrderItems] ADD [ReturnTrackingNumber] NVARCHAR(MAX) NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('store.OrderItems', 'RefundRequestedAt') IS NOT NULL
                    ALTER TABLE [store].[OrderItems] DROP COLUMN [RefundRequestedAt];

                IF COL_LENGTH('store.OrderItems', 'RefundRequestReason') IS NOT NULL
                    ALTER TABLE [store].[OrderItems] DROP COLUMN [RefundRequestReason];

                IF COL_LENGTH('store.OrderItems', 'ReturnTrackingNumber') IS NOT NULL
                    ALTER TABLE [store].[OrderItems] DROP COLUMN [ReturnTrackingNumber];
                """);
        }
    }
}
