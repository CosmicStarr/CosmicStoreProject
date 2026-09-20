using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{
    /// <inheritdoc />
    public partial class ProductShortDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('store.GetProducts', 'ShortDescription') IS NULL
                    ALTER TABLE [store].[GetProducts] ADD [ShortDescription] NVARCHAR(MAX) NULL;

                IF OBJECT_ID('store.ProductWithPictureDto', 'U') IS NOT NULL
                   AND COL_LENGTH('store.ProductWithPictureDto', 'ShortDescription') IS NULL
                    ALTER TABLE [store].[ProductWithPictureDto] ADD [ShortDescription] NVARCHAR(MAX) NULL;
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

            migrationBuilder.Sql("""
                IF OBJECT_ID('store.ProductWithPictureDto', 'U') IS NOT NULL
                   AND COL_LENGTH('store.ProductWithPictureDto', 'ShortDescription') IS NOT NULL
                    ALTER TABLE [store].[ProductWithPictureDto] DROP COLUMN [ShortDescription];

                IF COL_LENGTH('store.GetProducts', 'ShortDescription') IS NOT NULL
                    ALTER TABLE [store].[GetProducts] DROP COLUMN [ShortDescription];
                """);
        }
    }
}
