namespace Data.Util;

public static class SqlConstants
{
    public static class StoredProcedures
    {
        public const string GetProductsWithPictures = "EXEC [store].[GetAllProductsWithPictures] @Category";
        public const string GetSingleProductWithPictures = "EXEC [store].[GetProductById] @ProductId";
        public const string GetHighlightedProducts = "EXEC [store].[GetHighlightedProducts] @HighlightType";

        // Add future stored procedures here...
    }
}