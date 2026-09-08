using Data.Interfaces;
using Data.Util;
using Models;
using Models.AngularDTOs;

namespace Data.Classes;

public class EditCjProducts(IUnitOfWork unitOfWork, IStoreUnitOfWork storeUnitOfWork) : IEditCjProducts
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IStoreUnitOfWork _storeUnitOfWork = storeUnitOfWork;

    public async Task<Products> EditCjProductAsync(string id, EditProductInfo product)
    {
        var existing = await _storeUnitOfWork.Repository<Products>()
            .GetFirstOrDefault(p => p.Id == id);

        Products storeProduct;

        if (existing is not null)
        {
            existing.NameEn = product.NameEn ?? existing.NameEn;
            existing.Sku = product.Sku ?? existing.Sku;
            existing.DescriptionEn = product.DescriptionEn ?? existing.DescriptionEn;
            existing.IsFeatured = product.IsFeatured;
            existing.IsNewArrival = product.IsNewArrival;
            existing.IsTopSelling = product.IsTopSelling;
            existing.SellPrice = product.SellPrice;
            existing.BigImage = product.BigImage ?? existing.BigImage;
            existing.Category = product.Category ?? existing.Category;
            existing.CjVariantId = id;
            if (product.StockQuantity > 0)
            {
                existing.StockQuantity = product.StockQuantity;
            }

            _storeUnitOfWork.Repository<Products>().Update(existing);
            storeProduct = existing;
        }
        else
        {
            storeProduct = new Products
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
                IsTopSelling = product.IsTopSelling,
                CjVariantId = id,
                StockQuantity = product.StockQuantity > 0 ? product.StockQuantity : 50
            };

            _storeUnitOfWork.Repository<Products>().Add(storeProduct);
        }

        await ReplaceProductImagesAsync(id, product.ProductImages);
        await _storeUnitOfWork.Complete();

        return storeProduct;
    }

    public async Task<Products> PublishFlatProductAsync(string flatProductId, decimal markupMultiplier = 1.4m)
    {
        var flat = await _unitOfWork.Repository<FlatProduct>()
            .GetFirstOrDefault(x => x.Id == flatProductId, "Category");

        if (flat is null)
            throw new InvalidOperationException($"CJ product '{flatProductId}' was not found.");

        var productInfo = MapFlatToEditInfo(flat, markupMultiplier);
        return await EditCjProductAsync(flatProductId, productInfo);
    }

    public async Task<IReadOnlyList<Products>> PublishBulkAsync(IEnumerable<string> flatProductIds, decimal markupMultiplier = 1.4m)
    {
        var published = new List<Products>();

        foreach (var id in flatProductIds.Distinct())
        {
            published.Add(await PublishFlatProductAsync(id, markupMultiplier));
        }

        return published;
    }

    public IEnumerable<ProductResponseDto> GroupData(IEnumerable<ProductWithPictureDto> flatData)
    {
        return flatData
            .GroupBy(p => p.Id)
            .Select(group => new ProductResponseDto
            {
                Id = group.Key,
                NameEn = group.First().NameEn,
                Sku = group.First().Sku,
                SellPrice = group.First().SellPrice,
                BigImage = group.First().BigImage,
                Category = group.First().Category,
                DescriptionEn = group.First().DescriptionEn,
                IsFeatured = group.First().IsFeatured,
                IsNewArrival = group.First().IsNewArrival,
                IsTopSelling = group.First().IsTopSelling,
                StockQuantity = group.First().StockQuantity,
                Pictures = group
                    .Where(x => x.PictureId != null)
                    .Select(x => new PictureDto
                    {
                        ProductId = x.PictureId,
                        PhotoUrl = x.PhotoUrl,
                        SkuPhoto = x.SkuPhoto
                    })
                    .ToList()
            });
    }

    private static EditProductInfo MapFlatToEditInfo(FlatProduct flat, decimal markupMultiplier)
    {
        var images = new List<ProductImage>();

        if (!string.IsNullOrWhiteSpace(flat.BigImage))
        {
            images.Add(new ProductImage
            {
                ProductId = flat.Id,
                PhotoUrl = flat.BigImage,
                SkuPhoto = flat.Sku
            });
        }

        return new EditProductInfo
        {
            Id = flat.Id,
            NameEn = flat.NameEn,
            Sku = flat.Sku,
            SellPrice = Math.Round(flat.SellPrice * markupMultiplier, 2),
            BigImage = flat.BigImage,
            Category = flat.Category?.CategoryName,
            StockQuantity = 50,
            ProductImages = images
        };
    }

    private async Task ReplaceProductImagesAsync(string productId, IList<ProductImage>? productImages)
    {
        var pageParams = new PageParams { PageNumber = 1, PageSize = 500 };
        var existingImages = await _storeUnitOfWork.Repository<ProductImage>()
            .GetAllParams(pageParams, img => img.ProductId == productId);

        foreach (var image in existingImages)
        {
            _storeUnitOfWork.Repository<ProductImage>().Remove(image);
        }

        foreach (var item in productImages ?? Array.Empty<ProductImage>())
        {
            if (string.IsNullOrWhiteSpace(item.PhotoUrl))
                continue;

            _storeUnitOfWork.Repository<ProductImage>().Add(new ProductImage
            {
                ProductId = productId,
                PhotoUrl = item.PhotoUrl,
                SkuPhoto = item.SkuPhoto ?? string.Empty
            });
        }
    }
}
