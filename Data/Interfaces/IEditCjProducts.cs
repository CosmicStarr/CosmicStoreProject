using Models;
using Models.AngularDTOs;

namespace Data.Interfaces;

public interface IEditCjProducts
{
    Task<ProductResponseDto> CreateManualProductAsync(EditProductInfo product);
    Task<ProductResponseDto?> GetStagingProductAsync(string id);
    Task<ProductResponseDto?> UpdateStagingProductAsync(string id, EditProductInfo product);
    Task<Products> EditCjProductAsync(string id, EditProductInfo product);
    Task<bool> DeleteStoreProductAsync(string productId);
    Task<Products?> DeleteProductImageAsync(string productId, int? pictureId, string? photoUrl);
    Task<ProductResponseDto?> DeleteStagingImageAsync(string productId, string? photoUrl);
    Task<Products> PublishFlatProductAsync(string flatProductId, decimal markupMultiplier = 1.4m, EditProductInfo? overlay = null);
    Task<IReadOnlyList<Products>> PublishBulkAsync(IEnumerable<string> flatProductIds, decimal markupMultiplier = 1.4m);
    IEnumerable<ProductResponseDto> GroupData(IEnumerable<ProductWithPictureDto> flatData);
}
