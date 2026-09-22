using Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    [DbContext(typeof(ApplicationDbStoreContext))]
    [Migration("20260921240000_GiftRegistryWishlist")]
    public partial class GiftRegistryWishlist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Each Sql() is a separate batch. SQL Server cannot compile DML against a
            // column added earlier in the same batch, which caused "Invalid column name 'WishlistId'".
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'store.Wishlists', N'U') IS NULL
                BEGIN
                    CREATE TABLE [store].[Wishlists] (
                        [Id] INT IDENTITY(1,1) NOT NULL,
                        [PublicId] NVARCHAR(32) NOT NULL,
                        [AppUserId] NVARCHAR(450) NOT NULL,
                        [Name] NVARCHAR(80) NOT NULL,
                        [ShippingAddressId] INT NULL,
                        [IsGiftRegistry] BIT NOT NULL CONSTRAINT [DF_Wishlists_IsGiftRegistry] DEFAULT 0,
                        [CreatedAt] DATETIME2 NOT NULL,
                        CONSTRAINT [PK_Wishlists] PRIMARY KEY ([Id]),
                        CONSTRAINT [AK_Wishlists_PublicId] UNIQUE ([PublicId]),
                        CONSTRAINT [AK_Wishlists_AppUserId] UNIQUE ([AppUserId]),
                        CONSTRAINT [FK_Wishlists_UserAddresses_ShippingAddressId]
                            FOREIGN KEY ([ShippingAddressId]) REFERENCES [store].[UserAddresses] ([Id])
                    );
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('store.WishlistItems', 'WishlistId') IS NULL
                    ALTER TABLE [store].[WishlistItems] ADD [WishlistId] INT NULL;
                """);

            migrationBuilder.Sql("""
                INSERT INTO [store].[Wishlists] ([PublicId], [AppUserId], [Name], [IsGiftRegistry], [CreatedAt])
                SELECT REPLACE(CONVERT(nvarchar(36), NEWID()), '-', ''), wi.[AppUserId], N'My Wishlist', 0, SYSUTCDATETIME()
                FROM [store].[WishlistItems] wi
                WHERE NOT EXISTS (
                    SELECT 1 FROM [store].[Wishlists] w WHERE w.[AppUserId] = wi.[AppUserId]
                )
                GROUP BY wi.[AppUserId];
                """);

            migrationBuilder.Sql("""
                UPDATE wi
                SET [WishlistId] = w.[Id]
                FROM [store].[WishlistItems] wi
                INNER JOIN [store].[Wishlists] w ON w.[AppUserId] = wi.[AppUserId]
                WHERE wi.[WishlistId] IS NULL;
                """);

            migrationBuilder.Sql("""
                DELETE FROM [store].[WishlistItems] WHERE [WishlistId] IS NULL;
                """);

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'store.WishlistItems')
                      AND name = N'WishlistId'
                      AND is_nullable = 1)
                BEGIN
                    ALTER TABLE [store].[WishlistItems] ALTER COLUMN [WishlistId] INT NOT NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE name = N'FK_WishlistItems_Wishlists_WishlistId')
                BEGIN
                    ALTER TABLE [store].[WishlistItems] WITH CHECK ADD CONSTRAINT [FK_WishlistItems_Wishlists_WishlistId]
                        FOREIGN KEY ([WishlistId]) REFERENCES [store].[Wishlists] ([Id]) ON DELETE CASCADE;
                END
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_WishlistItems_WishlistId'
                      AND object_id = OBJECT_ID(N'store.WishlistItems'))
                BEGIN
                    CREATE INDEX [IX_WishlistItems_WishlistId] ON [store].[WishlistItems] ([WishlistId]);
                END
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('store.Orders', 'WishlistId') IS NULL
                    ALTER TABLE [store].[Orders] ADD [WishlistId] INT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('store.Orders', 'WishlistId') IS NOT NULL
                    ALTER TABLE [store].[Orders] DROP COLUMN [WishlistId];
                """);

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE name = N'FK_WishlistItems_Wishlists_WishlistId')
                    ALTER TABLE [store].[WishlistItems] DROP CONSTRAINT [FK_WishlistItems_Wishlists_WishlistId];
                """);

            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_WishlistItems_WishlistId'
                      AND object_id = OBJECT_ID(N'store.WishlistItems'))
                    DROP INDEX [IX_WishlistItems_WishlistId] ON [store].[WishlistItems];
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH('store.WishlistItems', 'WishlistId') IS NOT NULL
                    ALTER TABLE [store].[WishlistItems] DROP COLUMN [WishlistId];
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'store.Wishlists', N'U') IS NOT NULL
                    DROP TABLE [store].[Wishlists];
                """);
        }
    }
}
