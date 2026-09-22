using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class ProductTypeEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID('store.ProductTypes', 'U') IS NULL
                BEGIN
                    CREATE TABLE [store].[ProductTypes]
                    (
                        [Id] INT IDENTITY(1,1) NOT NULL,
                        [ProductId] NVARCHAR(450) NOT NULL,
                        [Name] NVARCHAR(MAX) NOT NULL,
                        [Sku] NVARCHAR(MAX) NOT NULL,
                        CONSTRAINT [PK_ProductTypes] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_ProductTypes_GetProducts_ProductId]
                            FOREIGN KEY ([ProductId]) REFERENCES [store].[GetProducts]([Id]) ON DELETE CASCADE
                    );

                    CREATE INDEX [IX_ProductTypes_ProductId]
                        ON [store].[ProductTypes]([ProductId]);
                END

                IF COL_LENGTH('store.Pictures', 'ProductTypeId') IS NULL
                    ALTER TABLE [store].[Pictures] ADD [ProductTypeId] INT NULL;

                IF COL_LENGTH('store.Pictures', 'Type') IS NOT NULL
                BEGIN
                    INSERT INTO [store].[ProductTypes] ([ProductId], [Name], [Sku])
                    SELECT DISTINCT
                        pi.ProductId,
                        COALESCE(NULLIF(LTRIM(RTRIM(pi.Type)), ''), N''),
                        COALESCE(NULLIF(LTRIM(RTRIM(pi.SkuPhoto)), ''), N'')
                    FROM [store].[Pictures] pi
                    WHERE (
                            NULLIF(LTRIM(RTRIM(pi.Type)), '') IS NOT NULL
                         OR NULLIF(LTRIM(RTRIM(pi.SkuPhoto)), '') IS NOT NULL
                    )
                      AND NOT EXISTS (
                          SELECT 1
                          FROM [store].[ProductTypes] existing
                          WHERE existing.ProductId = pi.ProductId
                            AND existing.Name = COALESCE(NULLIF(LTRIM(RTRIM(pi.Type)), ''), N'')
                            AND existing.Sku = COALESCE(NULLIF(LTRIM(RTRIM(pi.SkuPhoto)), ''), N'')
                      );

                    UPDATE pi
                    SET ProductTypeId = t.Id
                    FROM [store].[Pictures] pi
                    INNER JOIN [store].[ProductTypes] t
                        ON t.ProductId = pi.ProductId
                       AND t.Name = COALESCE(NULLIF(LTRIM(RTRIM(pi.Type)), ''), N'')
                       AND t.Sku = COALESCE(NULLIF(LTRIM(RTRIM(pi.SkuPhoto)), ''), N'');
                END

                IF NOT EXISTS (
                    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Pictures_ProductTypes_ProductTypeId'
                )
                   AND COL_LENGTH('store.Pictures', 'ProductTypeId') IS NOT NULL
                BEGIN
                    ALTER TABLE [store].[Pictures] WITH CHECK
                    ADD CONSTRAINT [FK_Pictures_ProductTypes_ProductTypeId]
                    FOREIGN KEY ([ProductTypeId]) REFERENCES [store].[ProductTypes]([Id]);

                    CREATE INDEX [IX_Pictures_ProductTypeId]
                        ON [store].[Pictures]([ProductTypeId]);
                END

                IF COL_LENGTH('store.Pictures', 'Type') IS NOT NULL
                    ALTER TABLE [store].[Pictures] DROP COLUMN [Type];

                IF OBJECT_ID('store.ProductWithPictureDto', 'U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH('store.ProductWithPictureDto', 'ProductTypeId') IS NULL
                        ALTER TABLE [store].[ProductWithPictureDto] ADD [ProductTypeId] INT NULL;
                    IF COL_LENGTH('store.ProductWithPictureDto', 'TypeName') IS NULL
                        ALTER TABLE [store].[ProductWithPictureDto] ADD [TypeName] NVARCHAR(MAX) NULL;
                    IF COL_LENGTH('store.ProductWithPictureDto', 'TypeSku') IS NULL
                        ALTER TABLE [store].[ProductWithPictureDto] ADD [TypeSku] NVARCHAR(MAX) NULL;
                    IF COL_LENGTH('store.ProductWithPictureDto', 'Type') IS NOT NULL
                        ALTER TABLE [store].[ProductWithPictureDto] DROP COLUMN [Type];
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
                        p.ShortDescription,
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
                        pt.Id AS ProductTypeId,
                        pt.Name AS TypeName,
                        pt.Sku AS TypeSku
                    FROM [store].[GetProducts] p
                    LEFT JOIN [store].[Pictures] pi ON p.Id = pi.ProductId
                    LEFT JOIN [store].[ProductTypes] pt ON pi.ProductTypeId = pt.Id
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
                        p.ShortDescription,
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
                        pt.Id AS ProductTypeId,
                        pt.Name AS TypeName,
                        pt.Sku AS TypeSku
                    FROM [store].[GetProducts] p
                    LEFT JOIN [store].[Pictures] pi ON p.Id = pi.ProductId
                    LEFT JOIN [store].[ProductTypes] pt ON pi.ProductTypeId = pt.Id
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
                        p.ShortDescription,
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
                        pt.Id AS ProductTypeId,
                        pt.Name AS TypeName,
                        pt.Sku AS TypeSku
                    FROM [store].[GetProducts] p
                    LEFT JOIN [store].[Pictures] pi ON p.Id = pi.ProductId
                    LEFT JOIN [store].[ProductTypes] pt ON pi.ProductTypeId = pt.Id
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
                IF COL_LENGTH('store.Pictures', 'Type') IS NULL
                    ALTER TABLE [store].[Pictures] ADD [Type] NVARCHAR(MAX) NULL;

                IF COL_LENGTH('store.Pictures', 'ProductTypeId') IS NOT NULL
                BEGIN
                    UPDATE pi
                    SET Type = NULLIF(t.Name, N'')
                    FROM [store].[Pictures] pi
                    INNER JOIN [store].[ProductTypes] t ON t.Id = pi.ProductTypeId;
                END

                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Pictures_ProductTypes_ProductTypeId')
                    ALTER TABLE [store].[Pictures] DROP CONSTRAINT [FK_Pictures_ProductTypes_ProductTypeId];

                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = 'IX_Pictures_ProductTypeId'
                      AND object_id = OBJECT_ID('store.Pictures')
                )
                    DROP INDEX [IX_Pictures_ProductTypeId] ON [store].[Pictures];

                IF COL_LENGTH('store.Pictures', 'ProductTypeId') IS NOT NULL
                    ALTER TABLE [store].[Pictures] DROP COLUMN [ProductTypeId];

                IF OBJECT_ID('store.ProductTypes', 'U') IS NOT NULL
                    DROP TABLE [store].[ProductTypes];
                """);
        }
    }
}
