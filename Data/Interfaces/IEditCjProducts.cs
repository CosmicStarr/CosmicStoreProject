using Models;
using Models.AngularDTOs;

namespace Data.Interfaces;

public interface IEditCjProducts
{
    Task<Products> EditCjProductAsync(string id, EditProductInfo product);
    
    IEnumerable<ProductResponseDto> GroupData(IEnumerable<ProductWithPictureDto> flatData);
}