using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class PictureType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('store.Pictures', 'Type') IS NULL
                    ALTER TABLE [store].[Pictures] ADD [Type] NVARCHAR(MAX) NULL;

                IF OBJECT_ID('store.ProductWithPictureDto', 'U') IS NOT NULL
                   AND COL_LENGTH('store.ProductWithPictureDto', 'Type') IS NULL
                    ALTER TABLE [store].[ProductWithPictureDto] ADD [Type] NVARCHAR(MAX) NULL;

                IF COL_LENGTH('store.Pictures', 'ProductsId') IS NOT NULL
                BEGIN
                    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Pictures_GetProducts_ProductsId')
                        ALTER TABLE [store].[Pictures] DROP CONSTRAINT [FK_Pictures_GetProducts_ProductsId];

                    IF EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE name = 'IX_Pictures_ProductsId'
                          AND object_id = OBJECT_ID('store.Pictures')
                    )
                        DROP INDEX [IX_Pictures_ProductsId] ON [store].[Pictures];

                    ALTER TABLE [store].[Pictures] DROP COLUMN [ProductsId];
                END

                IF COL_LENGTH('store.Pictures', 'ProductId') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Pictures_GetProducts_ProductId'
                   )
                BEGIN
                    ALTER TABLE [store].[Pictures] ALTER COLUMN [ProductId] NVARCHAR(450) NOT NULL;

                    IF NOT EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE name = 'IX_Pictures_ProductId'
                          AND object_id = OBJECT_ID('store.Pictures')
                    )
                        CREATE INDEX [IX_Pictures_ProductId] ON [store].[Pictures]([ProductId]);

                    ALTER TABLE [store].[Pictures] WITH CHECK
                    ADD CONSTRAINT [FK_Pictures_GetProducts_ProductId]
                    FOREIGN KEY ([ProductId]) REFERENCES [store].[GetProducts]([Id]) ON DELETE CASCADE;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE [store].[GetAllProductsWithPictures]
                    @Category NVARCHAR(MAX) = NULL
                AS
                BEGIN
                    SET NOCOUNT ON;

                    SELECT
                        p.Id,
                        p.NameEn,
                        p.Sku,
                        p.DescriptionEn,
                        p.IsFeatured,
                        p.IsNewArrival,
                        p.IsTopSelling,
                        p.SellPrice,
                        p.BigImage,
                        p.Category,
                        p.StockQuantity,
                        CAST(pi.Id AS NVARCHAR(50)) AS PictureId,
                        pi.PhotoUrl,
                        pi.SkuPhoto,
                        pi.Type
                    FROM [store].[GetProducts] p
                    LEFT JOIN [store].[Pictures] pi ON p.Id = pi.ProductId
                    WHERE (@Category IS NULL OR @Category = '' OR p.Category = @Category);
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE [store].[GetSingleProductWithPictures]
                    @ProductId NVARCHAR(450)
                AS
                BEGIN
                    SET NOCOUNT ON;

                    SELECT
                        p.Id,
                        p.NameEn,
                        p.Sku,
                        p.DescriptionEn,
                        p.IsFeatured,
                        p.IsNewArrival,
                        p.IsTopSelling,
                        p.SellPrice,
                        p.BigImage,
                        p.Category,
                        p.StockQuantity,
                        CAST(pi.Id AS NVARCHAR(50)) AS PictureId,
                        pi.PhotoUrl,
                        pi.SkuPhoto,
                        pi.Type
                    FROM [store].[GetProducts] p
                    LEFT JOIN [store].[Pictures] pi ON p.Id = pi.ProductId
                    WHERE p.Id = @ProductId;
                END
                """);

            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE [store].[GetHighlightedProducts]
                    @HighlightType NVARCHAR(50)
                AS
                BEGIN
                    SET NOCOUNT ON;

                    SELECT
                        p.Id,
                        p.NameEn,
                        p.Sku,
                        p.DescriptionEn,
                        p.IsFeatured,
                        p.IsNewArrival,
                        p.IsTopSelling,
                        p.SellPrice,
                        p.BigImage,
                        p.Category,
                        p.StockQuantity,
                        CAST(pi.Id AS NVARCHAR(50)) AS PictureId,
                        pi.PhotoUrl,
                        pi.SkuPhoto,
                        pi.Type
                    FROM [store].[GetProducts] p
                    LEFT JOIN [store].[Pictures] pi ON p.Id = pi.ProductId
                    WHERE
                        (@HighlightType = 'Featured' AND p.IsFeatured = 1)
                        OR (@HighlightType = 'NewArrival' AND p.IsNewArrival = 1)
                        OR (@HighlightType = 'TopSelling' AND p.IsTopSelling = 1);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Pictures_GetProducts_ProductId')
                    ALTER TABLE [store].[Pictures] DROP CONSTRAINT [FK_Pictures_GetProducts_ProductId];

                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_Pictures_ProductId'
                      AND object_id = OBJECT_ID('store.Pictures')
                )
                    DROP INDEX [IX_Pictures_ProductId] ON [store].[Pictures];

                IF COL_LENGTH('store.Pictures', 'Type') IS NOT NULL
                    ALTER TABLE [store].[Pictures] DROP COLUMN [Type];

                IF OBJECT_ID('store.ProductWithPictureDto', 'U') IS NOT NULL
                   AND COL_LENGTH('store.ProductWithPictureDto', 'Type') IS NOT NULL
                    ALTER TABLE [store].[ProductWithPictureDto] DROP COLUMN [Type];

                IF COL_LENGTH('store.Pictures', 'ProductId') IS NOT NULL
                    ALTER TABLE [store].[Pictures] ALTER COLUMN [ProductId] NVARCHAR(2083) NOT NULL;

                IF COL_LENGTH('store.Pictures', 'ProductsId') IS NULL
                    ALTER TABLE [store].[Pictures] ADD [ProductsId] NVARCHAR(450) NULL;

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_Pictures_ProductsId'
                      AND object_id = OBJECT_ID('store.Pictures')
                )
                    CREATE INDEX [IX_Pictures_ProductsId] ON [store].[Pictures]([ProductsId]);

                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Pictures_GetProducts_ProductsId')
                    ALTER TABLE [store].[Pictures] WITH CHECK
                    ADD CONSTRAINT [FK_Pictures_GetProducts_ProductsId]
                    FOREIGN KEY ([ProductsId]) REFERENCES [store].[GetProducts]([Id]);
                """);
        }
    }
}
