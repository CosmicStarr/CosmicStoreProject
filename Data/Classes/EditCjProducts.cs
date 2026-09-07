using System.Security.Cryptography.X509Certificates;
using Data.Interfaces;
using Models;
using Models.AngularDTOs;

namespace Data.Classes;

public class EditCjProducts(IUnitOfWork unitOfWork, IStoreUnitOfWork storeUnitOfWork) : IEditCjProducts
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    private readonly IStoreUnitOfWork _storeUnitOfWork = storeUnitOfWork;

    public async Task<Products> EditCjProductAsync(string id, EditProductInfo product)
    {
      
        foreach (var item in product.ProductImages ?? Array.Empty<ProductImage>())
        {
            var info = new ProductImage
            {
                ProductId = id,
                PhotoUrl = item.PhotoUrl,
                SkuPhoto = item.SkuPhoto
            };
            _storeUnitOfWork.Repository<ProductImage>().Add(info);
        }

        var newProduct = new Products
        {
                Id = id, 
                NameEn = product.NameEn ?? string.Empty,
                Sku = product.Sku ?? string.Empty,
                SellPrice = product.SellPrice,
                BigImage = product.BigImage,
                Category = product.Category,
                DescriptionEn = product.DescriptionEn,
                IsFeatured = product.IsFeatured,
                IsNewArrival = product.IsNewArrival, 
                IsTopSelling = product.IsTopSelling    
        };
            
            // Add the brand new product
        _storeUnitOfWork.Repository<Products>().Add(newProduct);
        await _storeUnitOfWork.Complete();
            
        return newProduct;
        
    }

    public IEnumerable<ProductResponseDto> GroupData(IEnumerable<ProductWithPictureDto> flatData)
    {
        var nestedProducts = flatData
        .GroupBy(p => p.Id)
        .Select(group => new ProductResponseDto
        {
            Id = group.Key,
            
            // Product details are duplicated across rows, so just grab them from the First() row in the group
            NameEn = group.First().NameEn,
            Sku = group.First().Sku,
            SellPrice = group.First().SellPrice,
            BigImage = group.First().BigImage,
            Category = group.First().Category,
            DescriptionEn = group.First().DescriptionEn,
            IsFeatured = group.First().IsFeatured,
            IsNewArrival = group.First().IsNewArrival,  
            IsTopSelling = group.First().IsTopSelling,
            
            // Map all the pictures in this group into a single List
                Pictures = group
                .Where(x => x.PictureId != null) // Filter out nulls from the LEFT JOIN if a product has 0 pictures
                .Select(x => new PictureDto
                {
                    ProductId = x.PictureId,
                    PhotoUrl = x.PhotoUrl,
                    SkuPhoto = x.SkuPhoto
                })
                .ToList()
        });

        // 3. Return the clean, nested JSON
        return nestedProducts;
    }
}