using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class ProductTypePrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID('store.ProductTypes', 'U') IS NOT NULL
                   AND COL_LENGTH('store.ProductTypes', 'Price') IS NULL
                    ALTER TABLE [store].[ProductTypes]
                    ADD [Price] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_ProductTypes_Price] DEFAULT (0);

                IF OBJECT_ID('store.ProductWithPictureDto', 'U') IS NOT NULL
                   AND COL_LENGTH('store.ProductWithPictureDto', 'TypePrice') IS NULL
                    ALTER TABLE [store].[ProductWithPictureDto] ADD [TypePrice] DECIMAL(18,2) NOT NULL DEFAULT (0);
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
                        pt.Sku AS TypeSku,
                        ISNULL(pt.Price, 0) AS TypePrice
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
                        pt.Sku AS TypeSku,
                        ISNULL(pt.Price, 0) AS TypePrice
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
                        pt.Sku AS TypeSku,
                        ISNULL(pt.Price, 0) AS TypePrice
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
                IF OBJECT_ID('store.ProductTypes', 'U') IS NOT NULL
                   AND COL_LENGTH('store.ProductTypes', 'Price') IS NOT NULL
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM sys.default_constraints
                        WHERE name = 'DF_ProductTypes_Price'
                    )
                        ALTER TABLE [store].[ProductTypes] DROP CONSTRAINT [DF_ProductTypes_Price];

                    ALTER TABLE [store].[ProductTypes] DROP COLUMN [Price];
                END

                IF OBJECT_ID('store.ProductWithPictureDto', 'U') IS NOT NULL
                   AND COL_LENGTH('store.ProductWithPictureDto', 'TypePrice') IS NOT NULL
                    ALTER TABLE [store].[ProductWithPictureDto] DROP COLUMN [TypePrice];
                """);
        }
    }
}
