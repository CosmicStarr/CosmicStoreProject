using Models;
using Models.AngularDTOs;

namespace Data.Interfaces;

public interface IEditCjProducts
{
    Task<ProductResponseDto> CreateManualProductAsync(EditProductInfo product);
    Task<ProductResponseDto?> GetStagingProductAsync(string id);
    Task<ProductResponseDto?> UpdateStagingProductAsync(string id, EditProductInfo product);
    Task<Products> EditCjProductAsync(string id, EditProductInfo product);
    /// <summary>Removes the published storefront row only; keeps <c>dbo.FlatProducts</c>.</summary>
    Task<bool> UnpublishStoreProductAsync(string productId);
    /// <summary>Removes storefront (if any) and the <c>dbo.FlatProducts</c> staging row.</summary>
    Task<bool> DeleteStoreProductAsync(string productId);
    Task<Products?> DeleteProductImageAsync(string productId, int? pictureId, string? photoUrl, string? skuPhoto = null);
    Task<ProductResponseDto?> DeleteStagingImageAsync(string productId, string? photoUrl, string? skuPhoto = null);
    Task<Products> PublishFlatProductAsync(string flatProductId, decimal markupMultiplier = 1.4m, EditProductInfo? overlay = null);
    Task<IReadOnlyList<Products>> PublishBulkAsync(IEnumerable<string> flatProductIds, decimal markupMultiplier = 1.4m);
    /// <summary>Clears New Arrival on products marked more than 7 days ago. Returns how many were cleared.</summary>
    Task<int> ExpireStaleNewArrivalsAsync();
    IEnumerable<ProductResponseDto> GroupData(IEnumerable<ProductWithPictureDto> flatData);
}
