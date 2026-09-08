-- Store schema stored procedures for CosmicStore storefront API
-- Run against CosmicStoreProject database

IF OBJECT_ID('[store].[GetAllProductsWithPictures]', 'P') IS NOT NULL
    DROP PROCEDURE [store].[GetAllProductsWithPictures];
GO

CREATE PROCEDURE [store].[GetAllProductsWithPictures]
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
        pi.SkuPhoto
    FROM [store].[GetProducts] p
    LEFT JOIN [store].[Pictures] pi ON p.Id = pi.ProductId
    WHERE (@Category IS NULL OR @Category = '' OR p.Category = @Category);
END
GO

IF OBJECT_ID('[store].[GetSingleProductWithPictures]', 'P') IS NOT NULL
    DROP PROCEDURE [store].[GetSingleProductWithPictures];
GO

CREATE PROCEDURE [store].[GetSingleProductWithPictures]
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
        pi.SkuPhoto
    FROM [store].[GetProducts] p
    LEFT JOIN [store].[Pictures] pi ON p.Id = pi.ProductId
    WHERE p.Id = @ProductId;
END
GO

IF OBJECT_ID('[store].[GetHighlightedProducts]', 'P') IS NOT NULL
    DROP PROCEDURE [store].[GetHighlightedProducts];
GO

CREATE PROCEDURE [store].[GetHighlightedProducts]
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
        pi.SkuPhoto
    FROM [store].[GetProducts] p
    LEFT JOIN [store].[Pictures] pi ON p.Id = pi.ProductId
    WHERE
        (@HighlightType = 'Featured' AND p.IsFeatured = 1)
        OR (@HighlightType = 'NewArrival' AND p.IsNewArrival = 1)
        OR (@HighlightType = 'TopSelling' AND p.IsTopSelling = 1);
END
GO
