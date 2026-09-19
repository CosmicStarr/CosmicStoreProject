using Data.Interfaces;
using Data.Util;
using Models;
using Models.AngularDTOs;

namespace Data.Classes;

/// <summary>
/// Admin catalog writes: Add Product, edit published products, publish FlatProducts, and group storefront DTOs.
/// </summary>
public class EditCjProducts(
    IUnitOfWork unitOfWork,
    IStoreUnitOfWork storeUnitOfWork,
    ICJDropshippingService cjService) : IEditCjProducts
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IStoreUnitOfWork _storeUnitOfWork = storeUnitOfWork;
    private readonly ICJDropshippingService _cjService = cjService;

    /// <summary>
    /// Creates a storefront product with a new Guid. Optional CJ pid/variants become ProductVariant rows (vids).
    /// </summary>
    public async Task<Products> CreateManualProductAsync(EditProductInfo product)
    {
        if (string.IsNullOrWhiteSpace(product.NameEn) || string.IsNullOrWhiteSpace(product.Sku))
        {
            throw new InvalidOperationException("Name and SKU are required.");
        }

        var sku = product.Sku.Trim();
        var existingSku = await _storeUnitOfWork.Repository<Products>()
            .GetFirstOrDefault(p => p.Sku == sku);
        if (existingSku is not null)
        {
            throw new InvalidOperationException($"SKU '{sku}' is already in use.");
        }

        var variants = await ResolveVariantsAsync(product);
        var id = Guid.NewGuid().ToString();
        var images = product.ProductImages;

        if ((images is null || images.Count == 0) && variants.Count > 0)
        {
            images = variants
                .Where(variant => !string.IsNullOrWhiteSpace(variant.ImageUrl))
                .Select(variant => new PictureDto
                {
                    ProductId = id,
                    PhotoUrl = variant.ImageUrl,
                    SkuPhoto = string.IsNullOrWhiteSpace(variant.Sku) ? sku : variant.Sku,
                    Type = variant.VariantName
                })
                .ToList();
        }

        var storeProduct = new Products
        {
            Id = id,
            NameEn = product.NameEn.Trim(),
            Sku = sku,
            SellPrice = product.SellPrice,
            BigImage = FirstNonEmpty(product.BigImage, variants.FirstOrDefault()?.ImageUrl),
            Category = product.Category,
            DescriptionEn = product.DescriptionEn,
            IsFeatured = product.IsFeatured,
            IsNewArrival = product.IsNewArrival,
            IsTopSelling = product.IsTopSelling,
            CjVariantId = variants.FirstOrDefault()?.Vid,
            StockQuantity = product.StockQuantity > 0 ? product.StockQuantity : 50
        };

        _storeUnitOfWork.Repository<Products>().Add(storeProduct);
        await ReplaceProductImagesAsync(id, images);
        AddProductVariants(id, sku, variants);
        await _storeUnitOfWork.Complete();

        storeProduct.ProductImages = (images ?? [])
            .Where(image => !string.IsNullOrWhiteSpace(image.PhotoUrl))
            .Select(image => new ProductImage
            {
                ProductId = id,
                PhotoUrl = image.PhotoUrl!.Trim(),
                SkuPhoto = string.IsNullOrWhiteSpace(image.SkuPhoto) ? string.Empty : image.SkuPhoto,
                Type = string.IsNullOrWhiteSpace(image.Type) ? null : image.Type.Trim()
            })
            .ToList();

        return storeProduct;
    }

    /// <summary>Updates an existing storefront product, or inserts one when publishing a new FlatProduct id.</summary>
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

    /// <summary>Copies one dbo.FlatProduct into store.Products with markup, applying optional editor overrides.</summary>
    public async Task<Products> PublishFlatProductAsync(string flatProductId, decimal markupMultiplier = 1.4m, EditProductInfo? overlay = null)
    {
        var flat = await _unitOfWork.Repository<FlatProduct>()
            .GetFirstOrDefault(x => x.Id == flatProductId, "Category");

        if (flat is null)
            throw new InvalidOperationException($"CJ product '{flatProductId}' was not found.");

        var productInfo = MapFlatToEditInfo(flat, markupMultiplier);
        ApplyOverlay(productInfo, overlay);
        return await EditCjProductAsync(flatProductId, productInfo);
    }

    /// <summary>Publishes many staging products to the storefront in sequence.</summary>
    public async Task<IReadOnlyList<Products>> PublishBulkAsync(IEnumerable<string> flatProductIds, decimal markupMultiplier = 1.4m)
    {
        var published = new List<Products>();

        foreach (var id in flatProductIds.Distinct())
        {
            published.Add(await PublishFlatProductAsync(id, markupMultiplier));
        }

        return published;
    }

    /// <summary>
    /// Collapses stored-procedure rows (one row per picture) into one ProductResponseDto per product.
    /// </summary>
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
                    .Where(x => !string.IsNullOrWhiteSpace(x.PhotoUrl))
                    .Select(x => new PictureDto
                    {
                        ProductId = group.Key,
                        PhotoUrl = x.PhotoUrl,
                        SkuPhoto = x.SkuPhoto,
                        Type = x.Type
                    })
                    .GroupBy(x => x.PhotoUrl)
                    .Select(x => x.First())
                    .ToList()
            });
    }

    /// <summary>Maps a tracked Products entity (plus gallery) to the storefront/admin API shape.</summary>
    public static ProductResponseDto ToResponse(Products product)
    {
        var pictures = (product.ProductImages ?? [])
            .Where(image => !string.IsNullOrWhiteSpace(image.PhotoUrl))
            .Select(image => new PictureDto
            {
                ProductId = product.Id,
                PhotoUrl = image.PhotoUrl,
                SkuPhoto = image.SkuPhoto,
                Type = image.Type
            })
            .GroupBy(image => image.PhotoUrl)
            .Select(group => group.First())
            .ToList();

        return new ProductResponseDto
        {
            Id = product.Id,
            NameEn = product.NameEn,
            Sku = product.Sku,
            SellPrice = product.SellPrice,
            BigImage = product.BigImage,
            Category = product.Category,
            DescriptionEn = product.DescriptionEn,
            IsFeatured = product.IsFeatured,
            IsNewArrival = product.IsNewArrival,
            IsTopSelling = product.IsTopSelling,
            StockQuantity = product.StockQuantity,
            Pictures = pictures
        };
    }

    /// <summary>
    /// Fills missing gallery rows and Type values from stored CJ variants (image + variant name).
    /// </summary>
    public static void MergeVariantPictures(ProductResponseDto response, IEnumerable<ProductVariant> variants)
    {
        foreach (var variant in variants)
        {
            var imageUrl = variant.ImageUrl?.Trim();
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                continue;
            }

            var existing = response.Pictures.FirstOrDefault(picture =>
                string.Equals(picture.PhotoUrl?.Trim(), imageUrl, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(variant.Sku)
                    && string.Equals(picture.SkuPhoto?.Trim(), variant.Sku.Trim(), StringComparison.OrdinalIgnoreCase)));

            if (existing is not null)
            {
                if (string.IsNullOrWhiteSpace(existing.Type))
                {
                    existing.Type = variant.VariantName;
                }

                if (string.IsNullOrWhiteSpace(existing.SkuPhoto))
                {
                    existing.SkuPhoto = variant.Sku;
                }

                continue;
            }

            response.Pictures.Add(new PictureDto
            {
                ProductId = response.Id,
                PhotoUrl = imageUrl,
                SkuPhoto = variant.Sku,
                Type = variant.VariantName
            });
        }
    }

    /// <summary>Copies non-empty editor fields over the values mapped from a FlatProduct before publish.</summary>
    private static void ApplyOverlay(EditProductInfo productInfo, EditProductInfo? overlay)
    {
        if (overlay is null) return;

        var hasEditorFields = !string.IsNullOrWhiteSpace(overlay.NameEn)
            || overlay.ProductImages?.Count > 0
            || !string.IsNullOrWhiteSpace(overlay.DescriptionEn)
            || overlay.IsFeatured
            || overlay.IsNewArrival
            || overlay.IsTopSelling;

        if (!hasEditorFields) return;

        productInfo.IsFeatured = overlay.IsFeatured;
        productInfo.IsNewArrival = overlay.IsNewArrival;
        productInfo.IsTopSelling = overlay.IsTopSelling;

        if (!string.IsNullOrWhiteSpace(overlay.NameEn))
            productInfo.NameEn = overlay.NameEn;

        if (!string.IsNullOrWhiteSpace(overlay.Sku))
            productInfo.Sku = overlay.Sku;

        if (!string.IsNullOrWhiteSpace(overlay.DescriptionEn))
            productInfo.DescriptionEn = overlay.DescriptionEn;

        if (!string.IsNullOrWhiteSpace(overlay.BigImage))
            productInfo.BigImage = overlay.BigImage;

        if (!string.IsNullOrWhiteSpace(overlay.Category))
            productInfo.Category = overlay.Category;

        if (overlay.SellPrice > 0)
            productInfo.SellPrice = overlay.SellPrice;

        if (overlay.StockQuantity > 0)
            productInfo.StockQuantity = overlay.StockQuantity;

        var overlayImages = overlay.ProductImages?
            .Where(image => !string.IsNullOrWhiteSpace(image.PhotoUrl))
            .ToList();

        if (overlayImages is { Count: > 0 })
            productInfo.ProductImages = overlayImages;
    }

    /// <summary>Uses variants from the Add Product payload, or fetches them from CJ when only a pid is present.</summary>
    private async Task<IReadOnlyList<CjVariantDto>> ResolveVariantsAsync(EditProductInfo product)
    {
        var provided = (product.Variants ?? [])
            .Where(variant => !string.IsNullOrWhiteSpace(variant.Vid))
            .ToList();

        if (provided.Count > 0)
            return provided;

        var pid = product.CjProductId?.Trim();
        if (string.IsNullOrWhiteSpace(pid))
            return Array.Empty<CjVariantDto>();

        return await _cjService.GetProductVariantsAsync(pid);
    }

    /// <summary>Stages ProductVariant rows (vid + SKU) for a newly created storefront product.</summary>
    private void AddProductVariants(string productId, string fallbackSku, IReadOnlyList<CjVariantDto> variants)
    {
        foreach (var variant in variants)
        {
            _storeUnitOfWork.Repository<ProductVariant>().Add(new ProductVariant
            {
                ProductId = productId,
                CjVariantId = variant.Vid.Trim(),
                Sku = string.IsNullOrWhiteSpace(variant.Sku) ? fallbackSku : variant.Sku.Trim(),
                VariantName = variant.VariantName,
                CjPrice = variant.SellPrice,
                ImageUrl = variant.ImageUrl,
                LastSyncedAt = DateTime.UtcNow
            });
        }
    }

    /// <summary>Returns the first non-blank string, trimmed.</summary>
    private static string? FirstNonEmpty(params string?[] values)
    {
        var match = values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        return match?.Trim();
    }

    /// <summary>Builds the publish payload from a staging row, applying the storefront markup to sell price.</summary>
    private static EditProductInfo MapFlatToEditInfo(FlatProduct flat, decimal markupMultiplier)
    {
        var images = new List<PictureDto>();

        if (!string.IsNullOrWhiteSpace(flat.BigImage))
        {
            images.Add(new PictureDto
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
            IsFeatured = true,
            ProductImages = images
        };
    }

    /// <summary>Deletes existing gallery rows for a product and inserts the new picture list.</summary>
    private async Task ReplaceProductImagesAsync(string productId, IList<PictureDto>? productImages)
    {
        var pageParams = new PageParams { PageNumber = 1, PageSize = 500 };
        var existingImages = await _storeUnitOfWork.Repository<ProductImage>()
            .GetAllParams(pageParams, img => img.ProductId == productId);

        foreach (var image in existingImages)
        {
            _storeUnitOfWork.Repository<ProductImage>().Remove(image);
        }

        foreach (var item in productImages ?? Array.Empty<PictureDto>())
        {
            if (string.IsNullOrWhiteSpace(item.PhotoUrl))
                continue;

            _storeUnitOfWork.Repository<ProductImage>().Add(new ProductImage
            {
                ProductId = productId,
                PhotoUrl = item.PhotoUrl.Trim(),
                SkuPhoto = string.IsNullOrWhiteSpace(item.SkuPhoto) ? string.Empty : item.SkuPhoto,
                Type = string.IsNullOrWhiteSpace(item.Type) ? null : item.Type.Trim()
            });
        }
    }
}
