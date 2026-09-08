using Models;
using Models.AngularDTOs;

namespace Data.Interfaces;

public interface IEditCjProducts
{
    Task<Products> EditCjProductAsync(string id, EditProductInfo product);
    Task<Products> PublishFlatProductAsync(string flatProductId, decimal markupMultiplier = 1.4m);
    Task<IReadOnlyList<Products>> PublishBulkAsync(IEnumerable<string> flatProductIds, decimal markupMultiplier = 1.4m);
    IEnumerable<ProductResponseDto> GroupData(IEnumerable<ProductWithPictureDto> flatData);
}
