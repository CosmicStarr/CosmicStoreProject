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
    ICJDropshippingService cjService,
    ICacheService cacheService) : IEditCjProducts
{
    private static readonly TimeSpan StagingOverlayTtl = TimeSpan.FromDays(30);
    private const int NewArrivalDays = 7;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IStoreUnitOfWork _storeUnitOfWork = storeUnitOfWork;
    private readonly ICJDropshippingService _cjService = cjService;
    private readonly ICacheService _cacheService = cacheService;

    /// <summary>
    /// Adds a product to <c>dbo.FlatProducts</c> so it appears on the admin dashboard.
    /// It is not copied to the storefront until Publish.
    /// </summary>
    public async Task<ProductResponseDto> CreateManualProductAsync(EditProductInfo product)
    {
        if (string.IsNullOrWhiteSpace(product.NameEn) || string.IsNullOrWhiteSpace(product.Sku))
        {
            throw new InvalidOperationException("Name and SKU are required.");
        }

        var sku = product.Sku.Trim();
        var storeSku = await _storeUnitOfWork.Repository<Products>()
            .GetFirstOrDefault(p => p.Sku == sku);
        if (storeSku is not null)
        {
            throw new InvalidOperationException($"SKU '{sku}' is already on the storefront.");
        }

        var cjPid = product.CjProductId?.Trim();
        var id = !string.IsNullOrWhiteSpace(cjPid) ? cjPid : Guid.NewGuid().ToString();

        var existingStaging = await _unitOfWork.Repository<FlatProduct>()
            .GetFirstOrDefault(item => item.Id == id);
        if (existingStaging is null)
        {
            var skuTaken = await _unitOfWork.Repository<FlatProduct>()
                .GetFirstOrDefault(item => item.Sku == sku);
            if (skuTaken is not null)
            {
                throw new InvalidOperationException($"SKU '{sku}' is already in the admin catalog.");
            }
        }

        var variants = await ResolveVariantsAsync(product);
        var images = NormalizeImages(id, sku, product.ProductImages, variants);
        var bigImage = FirstNonEmpty(product.BigImage, images.FirstOrDefault()?.PhotoUrl);
        var categoryId = await ResolveCategoryIdAsync(product.Category);

        if (existingStaging is not null)
        {
            existingStaging.NameEn = product.NameEn.Trim();
            existingStaging.Sku = sku;
            existingStaging.SellPrice = product.SellPrice;
            existingStaging.BigImage = bigImage;
            existingStaging.CategoryId = categoryId;
            _unitOfWork.Repository<FlatProduct>().Update(existingStaging);
        }
        else
        {
            _unitOfWork.Repository<FlatProduct>().Add(new FlatProduct
            {
                Id = id,
                NameEn = product.NameEn.Trim(),
                Sku = sku,
                SellPrice = product.SellPrice,
                BigImage = bigImage,
                CategoryId = categoryId
            });
        }

        await _unitOfWork.Complete();
        await SaveStagingOverlayAsync(id, product, images, variants);

        return (await GetStagingProductAsync(id))!;
    }

    /// <summary>Loads a <c>dbo.FlatProducts</c> row plus any saved editor overlay (gallery, description, flags).</summary>
    public async Task<ProductResponseDto?> GetStagingProductAsync(string id)
    {
        var staging = await _unitOfWork.Repository<FlatProduct>()
            .GetFirstOrDefault(item => item.Id == id, "Category");
        if (staging is null)
        {
            return null;
        }

        return ToStagingResponse(staging, await GetStagingOverlayAsync(id));
    }

    /// <summary>Updates a staging catalog row without publishing it to the storefront.</summary>
    public async Task<ProductResponseDto?> UpdateStagingProductAsync(string id, EditProductInfo product)
    {
        var existing = await _unitOfWork.Repository<FlatProduct>()
            .GetFirstOrDefault(item => item.Id == id);
        if (existing is null)
        {
            return null;
        }

        var sku = string.IsNullOrWhiteSpace(product.Sku) ? existing.Sku : product.Sku.Trim();
        var variants = await ResolveVariantsAsync(product);
        var images = NormalizeImages(id, sku, product.ProductImages, variants);
        var bigImage = FirstNonEmpty(product.BigImage, images.FirstOrDefault()?.PhotoUrl, existing.BigImage);

        existing.NameEn = string.IsNullOrWhiteSpace(product.NameEn) ? existing.NameEn : product.NameEn.Trim();
        existing.Sku = sku;
        if (product.SellPrice > 0)
        {
            existing.SellPrice = product.SellPrice;
        }
        existing.BigImage = bigImage;
        existing.CategoryId = await ResolveCategoryIdAsync(product.Category) ?? existing.CategoryId;
        _unitOfWork.Repository<FlatProduct>().Update(existing);
        await _unitOfWork.Complete();
        await SaveStagingOverlayAsync(id, product, images, variants);

        return await GetStagingProductAsync(id);
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
            existing.ShortDescription = NormalizeOptional(product.ShortDescription);
            existing.IsFeatured = product.IsFeatured;
            ApplyNewArrivalFlag(existing, product.IsNewArrival);
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
                ShortDescription = NormalizeOptional(product.ShortDescription),
                IsFeatured = product.IsFeatured,
                IsNewArrival = product.IsNewArrival,
                NewArrivalMarkedAt = product.IsNewArrival ? DateTime.UtcNow : null,
                IsTopSelling = product.IsTopSelling,
                CjVariantId = id,
                StockQuantity = product.StockQuantity > 0 ? product.StockQuantity : 50
            };

            _storeUnitOfWork.Repository<Products>().Add(storeProduct);
        }

        await ReplaceProductTypesAndImagesAsync(id, product.ProductImages);
        await _storeUnitOfWork.Complete();

        return await LoadStoreProductGraphAsync(id) ?? storeProduct;
    }

    /// <summary>Removes the published storefront product only; leaves <c>dbo.FlatProducts</c> for re-publish.</summary>
    public async Task<bool> UnpublishStoreProductAsync(string productId)
    {
        var existing = await _storeUnitOfWork.Repository<Products>()
            .GetFirstOrDefault(product => product.Id == productId);

        if (existing is null)
        {
            return false;
        }

        _storeUnitOfWork.Repository<Products>().Remove(existing);
        await _storeUnitOfWork.Complete();
        return true;
    }

    /// <summary>Removes the storefront product if published, and the <c>dbo.FlatProducts</c> staging row.</summary>
    public async Task<bool> DeleteStoreProductAsync(string productId)
    {
        var deleted = false;
        var existing = await _storeUnitOfWork.Repository<Products>()
            .GetFirstOrDefault(product => product.Id == productId);

        if (existing is not null)
        {
            _storeUnitOfWork.Repository<Products>().Remove(existing);
            await _storeUnitOfWork.Complete();
            deleted = true;
        }

        var staging = await _unitOfWork.Repository<FlatProduct>()
            .GetFirstOrDefault(product => product.Id == productId);
        if (staging is not null)
        {
            _unitOfWork.Repository<FlatProduct>().Remove(staging);
            await _unitOfWork.Complete();
            deleted = true;
        }

        await _cacheService.RemoveData(StagingOverlayKey(productId));
        return deleted;
    }

    /// <summary>Removes one gallery row. If it was the main image, the next remaining photo becomes BigImage.</summary>
    public async Task<Products?> DeleteProductImageAsync(string productId, int? pictureId, string? photoUrl, string? skuPhoto = null)
    {
        var product = await _storeUnitOfWork.Repository<Products>()
            .GetFirstOrDefault(item => item.Id == productId);
        if (product is null)
        {
            return null;
        }

        var images = await _storeUnitOfWork.Repository<ProductImage>()
            .GetAllParams(
                new PageParams { PageNumber = 1, PageSize = 500 },
                image => image.ProductId == productId,
                includeProperties: "ProductType");

        ProductImage? match = null;
        if (pictureId is > 0)
        {
            match = images.FirstOrDefault(image => image.Id == pictureId.Value);
        }

        if (match is null && !string.IsNullOrWhiteSpace(photoUrl))
        {
            var url = photoUrl.Trim();
            var sku = skuPhoto?.Trim() ?? string.Empty;

            // Prefer the SKU-matched row when several images share the URL.
            if (!string.IsNullOrWhiteSpace(sku))
            {
                match = images.FirstOrDefault(image =>
                    string.Equals(image.PhotoUrl?.Trim(), url, StringComparison.OrdinalIgnoreCase)
                    && (string.Equals(image.SkuPhoto?.Trim(), sku, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(image.ProductType?.Sku?.Trim(), sku, StringComparison.OrdinalIgnoreCase)));
            }

            match ??= images.FirstOrDefault(image =>
                string.Equals(image.PhotoUrl?.Trim(), url, StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(image.SkuPhoto)
                && image.ProductTypeId is null or 0);

            match ??= images.FirstOrDefault(image =>
                string.Equals(image.PhotoUrl?.Trim(), url, StringComparison.OrdinalIgnoreCase));
        }

        if (match is null)
        {
            return null;
        }

        _storeUnitOfWork.Repository<ProductImage>().Remove(match);

        if (string.Equals(product.BigImage?.Trim(), match.PhotoUrl?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            // Only clear BigImage when no other gallery row still uses that URL.
            var urlStillUsed = images.Any(image =>
                image.Id != match.Id
                && string.Equals(image.PhotoUrl?.Trim(), match.PhotoUrl?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (!urlStillUsed)
            {
                product.BigImage = images
                    .Where(image => image.Id != match.Id)
                    .Select(image => image.PhotoUrl)
                    .FirstOrDefault(url => !string.IsNullOrWhiteSpace(url));
                _storeUnitOfWork.Repository<Products>().Update(product);
            }
        }

        await _storeUnitOfWork.Complete();

        return await LoadStoreProductGraphAsync(productId) ?? product;
    }

    /// <summary>Removes one overlay/gallery image from a staging product that is not on the storefront yet.</summary>
    public async Task<ProductResponseDto?> DeleteStagingImageAsync(string productId, string? photoUrl, string? skuPhoto = null)
    {
        var staging = await _unitOfWork.Repository<FlatProduct>()
            .GetFirstOrDefault(item => item.Id == productId);
        if (staging is null)
        {
            return null;
        }

        var url = photoUrl?.Trim();
        var sku = skuPhoto?.Trim() ?? string.Empty;
        var overlay = await GetStagingOverlayAsync(productId) ?? new EditProductInfo { Id = productId };
        var images = (overlay.ProductImages ?? [])
            .Where(image => !string.IsNullOrWhiteSpace(image.PhotoUrl))
            .ToList();

        var matchIndex = -1;
        for (var i = 0; i < images.Count; i++)
        {
            var image = images[i];
            if (!string.Equals(image.PhotoUrl?.Trim(), url, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var imageSku = image.SkuPhoto?.Trim()
                ?? image.ProductType?.Sku?.Trim()
                ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(sku)
                && !string.Equals(imageSku, sku, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            matchIndex = i;
            // When sku is provided, take the first SKU match; when not, take the first URL match.
            break;
        }

        if (matchIndex < 0
            && !string.IsNullOrWhiteSpace(url)
            && string.Equals(staging.BigImage?.Trim(), url, StringComparison.OrdinalIgnoreCase)
            && images.Count == 0)
        {
            staging.BigImage = null;
            _unitOfWork.Repository<FlatProduct>().Update(staging);
            await _unitOfWork.Complete();
            await _cacheService.ObjectToCache(StagingOverlayKey(productId), overlay, StagingOverlayTtl);
            return await GetStagingProductAsync(productId);
        }

        if (matchIndex < 0)
        {
            return null;
        }

        var removed = images[matchIndex];
        images.RemoveAt(matchIndex);

        if (string.Equals(staging.BigImage?.Trim(), url, StringComparison.OrdinalIgnoreCase))
        {
            var urlStillUsed = images.Any(image =>
                string.Equals(image.PhotoUrl?.Trim(), url, StringComparison.OrdinalIgnoreCase));
            if (!urlStillUsed)
            {
                staging.BigImage = images.FirstOrDefault()?.PhotoUrl;
            }
        }

        overlay.ProductImages = images;
        overlay.BigImage = staging.BigImage;
        _unitOfWork.Repository<FlatProduct>().Update(staging);
        await _unitOfWork.Complete();
        await _cacheService.ObjectToCache(StagingOverlayKey(productId), overlay, StagingOverlayTtl);
        return await GetStagingProductAsync(productId);
    }

    /// <summary>Copies one dbo.FlatProduct into store.Products with markup, applying optional editor overrides.</summary>
    public async Task<Products> PublishFlatProductAsync(string flatProductId, decimal markupMultiplier = 1.4m, EditProductInfo? overlay = null)
    {
        var flat = await _unitOfWork.Repository<FlatProduct>()
            .GetFirstOrDefault(x => x.Id == flatProductId, "Category");

        if (flat is null)
            throw new InvalidOperationException($"CJ product '{flatProductId}' was not found.");

        // Manual Add Product rows already store the intended sell price, not a CJ cost.
        var multiplier = Guid.TryParse(flatProductId, out _) ? 1m : markupMultiplier;
        var productInfo = MapFlatToEditInfo(flat, multiplier);
        ApplyOverlay(productInfo, await GetStagingOverlayAsync(flatProductId));
        ApplyOverlay(productInfo, overlay);
        var published = await EditCjProductAsync(flatProductId, productInfo);
        await _cacheService.RemoveData(StagingOverlayKey(flatProductId));
        return published;
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
    /// Turns off New Arrival for products marked more than 7 days ago (or with no mark date).
    /// </summary>
    public async Task<int> ExpireStaleNewArrivalsAsync()
    {
        var cutoff = DateTime.UtcNow.AddDays(-NewArrivalDays);
        var stale = await _storeUnitOfWork.Repository<Products>()
            .GetAllParams(
                new PageParams { PageNumber = 1, PageSize = 500 },
                product => product.IsNewArrival
                    && (product.NewArrivalMarkedAt == null || product.NewArrivalMarkedAt < cutoff));

        if (stale.Count == 0)
        {
            return 0;
        }

        foreach (var product in stale)
        {
            product.IsNewArrival = false;
            product.NewArrivalMarkedAt = null;
            _storeUnitOfWork.Repository<Products>().Update(product);
        }

        await _storeUnitOfWork.Complete();
        await _cacheService.RemoveData("products_all");
        return stale.Count;
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
                ShortDescription = NormalizeOptional(group.First().ShortDescription),
                IsFeatured = group.First().IsFeatured,
                IsNewArrival = group.First().IsNewArrival,
                IsTopSelling = group.First().IsTopSelling,
                StockQuantity = group.First().StockQuantity,
                Pictures = group
                    .Where(x => !string.IsNullOrWhiteSpace(x.PhotoUrl))
                    .Select(x => new PictureDto
                    {
                        Id = int.TryParse(x.PictureId, out var pictureId) ? pictureId : 0,
                        ProductId = group.Key,
                        PhotoUrl = x.PhotoUrl,
                        SkuPhoto = x.SkuPhoto,
                        ProductTypeId = x.ProductTypeId,
                        ProductType = MapTypeDto(group.Key, x.ProductTypeId, x.TypeName, x.TypeSku, x.TypePrice)
                    })
                    // Keep variants that share a photo URL as separate rows (keyed by picture id or URL+SKU).
                    .GroupBy(PictureIdentityKey, StringComparer.OrdinalIgnoreCase)
                    .Select(x => x.First())
                    .ToList()
            })
            .Select(product =>
            {
                product.Types = DistinctTypes(product.Pictures);
                return product;
            });
    }

    /// <summary>Maps a tracked Products entity (plus gallery) to the storefront/admin API shape.</summary>
    public static ProductResponseDto ToResponse(Products product)
    {
        var pictures = (product.ProductImages ?? [])
            .Where(image => !string.IsNullOrWhiteSpace(image.PhotoUrl))
            .Select(image => new PictureDto
            {
                Id = image.Id,
                ProductId = product.Id,
                PhotoUrl = image.PhotoUrl,
                SkuPhoto = image.SkuPhoto,
                ProductTypeId = image.ProductTypeId,
                ProductType = MapTypeDto(product.Id, image.ProductType)
            })
            // Do not collapse by PhotoUrl alone — multiple variants may share one image URL.
            .GroupBy(PictureIdentityKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        var types = (product.ProductTypes ?? [])
            .Select(type => MapTypeDto(product.Id, type))
            .Where(type => type is not null)
            .Select(type => type!)
            .ToList();
        if (types.Count == 0)
        {
            types = DistinctTypes(pictures);
        }

        return new ProductResponseDto
        {
            Id = product.Id,
            NameEn = product.NameEn,
            Sku = product.Sku,
            SellPrice = product.SellPrice,
            BigImage = product.BigImage,
            Category = product.Category,
            DescriptionEn = product.DescriptionEn,
            ShortDescription = NormalizeOptional(product.ShortDescription),
            IsFeatured = product.IsFeatured,
            // Admin editor needs the stored bit; storefront expiry still runs via ExpireStaleNewArrivalsAsync.
            IsNewArrival = product.IsNewArrival,
            IsTopSelling = product.IsTopSelling,
            StockQuantity = product.StockQuantity,
            IsPublished = true,
            Types = types,
            Pictures = pictures
        };
    }

    /// <summary>
    /// Fills type/SKU on existing gallery rows from stored CJ variants.
    /// New variant pictures are added only when <paramref name="addMissingPictures"/> is true (admin editor).
    /// The public storefront never receives unsaved variants from a refresh.
    /// </summary>
    public static void MergeVariantPictures(
        ProductResponseDto response,
        IEnumerable<ProductVariant> variants,
        bool addMissingPictures = false)
    {
        foreach (var variant in variants)
        {
            var imageUrl = variant.ImageUrl?.Trim();
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                continue;
            }

            var variantSku = variant.Sku?.Trim() ?? string.Empty;

            // Prefer SKU identity so two variants with the same photo stay separate.
            var existing = !string.IsNullOrWhiteSpace(variantSku)
                ? response.Pictures.FirstOrDefault(picture =>
                    string.Equals(picture.SkuPhoto?.Trim(), variantSku, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(picture.ProductType?.Sku?.Trim(), variantSku, StringComparison.OrdinalIgnoreCase))
                : null;

            // Legacy gallery row with this URL but no SKU yet can absorb the first matching variant.
            existing ??= response.Pictures.FirstOrDefault(picture =>
                string.Equals(picture.PhotoUrl?.Trim(), imageUrl, StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(picture.SkuPhoto)
                && picture.ProductType is null);

            var type = MapTypeDto(response.Id, null, variant.VariantName, variant.Sku);
            if (existing is not null)
            {
                if (existing.ProductType is null && type is not null)
                {
                    existing.ProductType = type;
                }
                else if (existing.ProductType is not null && existing.ProductType.Price <= 0 && type is { Price: > 0 })
                {
                    existing.ProductType.Price = type.Price;
                }

                if (string.IsNullOrWhiteSpace(existing.SkuPhoto))
                {
                    existing.SkuPhoto = variant.Sku;
                }

                continue;
            }

            if (!addMissingPictures)
            {
                continue;
            }

            response.Pictures.Add(new PictureDto
            {
                ProductId = response.Id,
                PhotoUrl = imageUrl,
                SkuPhoto = variant.Sku,
                ProductType = type,
                IsStorefrontDraft = true
            });
        }

        if (addMissingPictures)
        {
            response.Types = DistinctTypes(response.Pictures, response.Types);
        }
    }

    /// <summary>Copies non-empty editor fields over the values mapped from a FlatProduct before publish.</summary>
    private static void ApplyOverlay(EditProductInfo productInfo, EditProductInfo? overlay)
    {
        if (overlay is null) return;

        // Booleans must always apply (including false). Do not gate them on "has editor fields"
        // or unchecking Featured/New Arrival/Top Selling is ignored on publish.
        productInfo.IsFeatured = overlay.IsFeatured;
        productInfo.IsNewArrival = overlay.IsNewArrival;
        productInfo.IsTopSelling = overlay.IsTopSelling;

        var hasEditorFields = !string.IsNullOrWhiteSpace(overlay.NameEn)
            || overlay.ProductImages?.Count > 0
            || !string.IsNullOrWhiteSpace(overlay.DescriptionEn)
            || !string.IsNullOrWhiteSpace(overlay.ShortDescription)
            || !string.IsNullOrWhiteSpace(overlay.BigImage)
            || !string.IsNullOrWhiteSpace(overlay.Category)
            || !string.IsNullOrWhiteSpace(overlay.Sku)
            || overlay.SellPrice > 0
            || overlay.StockQuantity > 0;

        if (!hasEditorFields) return;

        if (!string.IsNullOrWhiteSpace(overlay.NameEn))
            productInfo.NameEn = overlay.NameEn;

        if (!string.IsNullOrWhiteSpace(overlay.Sku))
            productInfo.Sku = overlay.Sku;

        if (!string.IsNullOrWhiteSpace(overlay.DescriptionEn))
            productInfo.DescriptionEn = overlay.DescriptionEn;

        if (!string.IsNullOrWhiteSpace(overlay.ShortDescription))
            productInfo.ShortDescription = overlay.ShortDescription;

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

    /// <summary>Sets or clears New Arrival and records when it was last turned on.</summary>
    private static void ApplyNewArrivalFlag(Products product, bool isNewArrival)
    {
        if (isNewArrival)
        {
            if (!product.IsNewArrival || product.NewArrivalMarkedAt is null)
            {
                product.NewArrivalMarkedAt = DateTime.UtcNow;
            }

            product.IsNewArrival = true;
            return;
        }

        product.IsNewArrival = false;
        product.NewArrivalMarkedAt = null;
    }

    private static bool IsActiveNewArrival(Products product) =>
        product.IsNewArrival
        && product.NewArrivalMarkedAt is { } markedAt
        && markedAt >= DateTime.UtcNow.AddDays(-NewArrivalDays);

    /// <summary>Returns the first non-blank string, trimmed.</summary>
    private static string? FirstNonEmpty(params string?[] values)
    {
        var match = values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        return match?.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string StagingOverlayKey(string productId) => $"staging_overlay_{productId}";

    private async Task<string?> ResolveCategoryIdAsync(string? categoryName)
    {
        var name = categoryName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var existing = await _unitOfWork.Repository<FlatCategory>()
            .GetFirstOrDefault(category => category.CategoryName == name);
        if (existing is not null)
        {
            return existing.CategoryId;
        }

        var category = new FlatCategory
        {
            CategoryId = Guid.NewGuid().ToString(),
            CategoryName = name,
            FullPath = name
        };
        _unitOfWork.Repository<FlatCategory>().Add(category);
        return category.CategoryId;
    }

    private static List<PictureDto> NormalizeImages(
        string productId,
        string sku,
        IList<PictureDto>? productImages,
        IReadOnlyList<CjVariantDto> variants)
    {
        var images = (productImages ?? [])
            .Where(image => !string.IsNullOrWhiteSpace(image.PhotoUrl))
            .Select(image => new PictureDto
            {
                ProductId = productId,
                PhotoUrl = image.PhotoUrl!.Trim(),
                SkuPhoto = string.IsNullOrWhiteSpace(image.SkuPhoto) ? sku : image.SkuPhoto.Trim(),
                ProductTypeId = image.ProductTypeId,
                ProductType = ResolvePictureType(productId, image, sku)
            })
            .ToList();

        if (images.Count > 0 || variants.Count == 0)
        {
            return images;
        }

        return variants
            .Where(variant => !string.IsNullOrWhiteSpace(variant.ImageUrl))
            .Select(variant => new PictureDto
            {
                ProductId = productId,
                PhotoUrl = variant.ImageUrl,
                SkuPhoto = string.IsNullOrWhiteSpace(variant.Sku) ? sku : variant.Sku,
                ProductType = MapTypeDto(productId, null, variant.VariantName, string.IsNullOrWhiteSpace(variant.Sku) ? sku : variant.Sku)
            })
            .ToList();
    }

    private async Task SaveStagingOverlayAsync(
        string id,
        EditProductInfo product,
        IList<PictureDto> images,
        IReadOnlyList<CjVariantDto> variants)
    {
        var overlay = new EditProductInfo
        {
            Id = id,
            NameEn = product.NameEn,
            Sku = product.Sku,
            DescriptionEn = product.DescriptionEn,
            ShortDescription = NormalizeOptional(product.ShortDescription),
            IsFeatured = product.IsFeatured,
            IsNewArrival = product.IsNewArrival,
            IsTopSelling = product.IsTopSelling,
            SellPrice = product.SellPrice,
            StockQuantity = product.StockQuantity > 0 ? product.StockQuantity : 50,
            BigImage = FirstNonEmpty(product.BigImage, images.FirstOrDefault()?.PhotoUrl),
            Category = product.Category,
            ProductImages = images,
            CjProductId = product.CjProductId,
            Variants = variants.ToList()
        };

        await _cacheService.ObjectToCache(StagingOverlayKey(id), overlay, StagingOverlayTtl);
    }

    private Task<EditProductInfo?> GetStagingOverlayAsync(string id) =>
        _cacheService.GetCachedObject<EditProductInfo>(StagingOverlayKey(id));

    private static ProductResponseDto ToStagingResponse(FlatProduct staging, EditProductInfo? overlay)
    {
        var pictures = (overlay?.ProductImages ?? [])
            .Where(image => !string.IsNullOrWhiteSpace(image.PhotoUrl))
            .Select(image => new PictureDto
            {
                ProductId = staging.Id,
                PhotoUrl = image.PhotoUrl,
                SkuPhoto = image.SkuPhoto,
                ProductTypeId = image.ProductTypeId,
                ProductType = ResolvePictureType(staging.Id, image, staging.Sku)
            })
            .ToList();

        if (pictures.Count == 0 && !string.IsNullOrWhiteSpace(staging.BigImage))
        {
            pictures.Add(new PictureDto
            {
                ProductId = staging.Id,
                PhotoUrl = staging.BigImage,
                SkuPhoto = staging.Sku
            });
        }

        return new ProductResponseDto
        {
            Id = staging.Id,
            NameEn = FirstNonEmpty(overlay?.NameEn, staging.NameEn),
            Sku = FirstNonEmpty(overlay?.Sku, staging.Sku),
            SellPrice = overlay is { SellPrice: > 0 } ? overlay.SellPrice : staging.SellPrice,
            BigImage = FirstNonEmpty(overlay?.BigImage, staging.BigImage),
            Category = FirstNonEmpty(overlay?.Category, staging.Category?.CategoryName),
            DescriptionEn = overlay?.DescriptionEn,
            ShortDescription = NormalizeOptional(overlay?.ShortDescription),
            IsFeatured = overlay?.IsFeatured ?? false,
            IsNewArrival = overlay?.IsNewArrival ?? false,
            IsTopSelling = overlay?.IsTopSelling ?? false,
            StockQuantity = overlay is { StockQuantity: > 0 } ? overlay.StockQuantity : 50,
            IsPublished = false,
            Types = DistinctTypes(pictures),
            Pictures = pictures
        };
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
            IsFeatured = false,
            IsNewArrival = false,
            IsTopSelling = false,
            ProductImages = images
        };
    }

    private async Task<Products?> LoadStoreProductGraphAsync(string id) =>
        await _storeUnitOfWork.Repository<Products>()
            .GetFirstOrDefault(item => item.Id == id, "ProductImages.ProductType,ProductTypes");

    /// <summary>Deletes existing types and gallery rows for a product and inserts the new lists.</summary>
    private async Task ReplaceProductTypesAndImagesAsync(string productId, IList<PictureDto>? productImages)
    {
        var pageParams = new PageParams { PageNumber = 1, PageSize = 500 };
        var existingImages = await _storeUnitOfWork.Repository<ProductImage>()
            .GetAllParams(pageParams, img => img.ProductId == productId);
        foreach (var image in existingImages)
        {
            _storeUnitOfWork.Repository<ProductImage>().Remove(image);
        }

        var existingTypes = await _storeUnitOfWork.Repository<ProductType>()
            .GetAllParams(pageParams, type => type.ProductId == productId);
        foreach (var type in existingTypes)
        {
            _storeUnitOfWork.Repository<ProductType>().Remove(type);
        }

        var types = new Dictionary<string, ProductType>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in productImages ?? Array.Empty<PictureDto>())
        {
            if (string.IsNullOrWhiteSpace(item.PhotoUrl))
                continue;

            var skuPhoto = string.IsNullOrWhiteSpace(item.SkuPhoto) ? string.Empty : item.SkuPhoto.Trim();
            var type = ResolvePictureType(productId, item, skuPhoto);
            ProductType? typeEntity = null;
            if (type is not null)
            {
                var key = $"{type.Name}\u001f{type.Sku}";
                if (!types.TryGetValue(key, out typeEntity))
                {
                    typeEntity = new ProductType
                    {
                        ProductId = productId,
                        Name = type.Name,
                        Sku = type.Sku,
                        Price = type.Price
                    };
                    types[key] = typeEntity;
                    _storeUnitOfWork.Repository<ProductType>().Add(typeEntity);
                }
                else if (type.Price > typeEntity.Price)
                {
                    typeEntity.Price = type.Price;
                }
            }

            _storeUnitOfWork.Repository<ProductImage>().Add(new ProductImage
            {
                ProductId = productId,
                PhotoUrl = item.PhotoUrl.Trim(),
                SkuPhoto = skuPhoto,
                ProductType = typeEntity
            });
        }
    }

    private static ProductTypeDto? ResolvePictureType(string? productId, PictureDto image, string? fallbackSku)
    {
        if (image.ProductType is not null
            && (!string.IsNullOrWhiteSpace(image.ProductType.Name) || !string.IsNullOrWhiteSpace(image.ProductType.Sku)))
        {
            return MapTypeDto(
                productId,
                image.ProductType.Id == 0 ? image.ProductTypeId : image.ProductType.Id,
                image.ProductType.Name,
                string.IsNullOrWhiteSpace(image.ProductType.Sku) ? fallbackSku : image.ProductType.Sku,
                image.ProductType.Price);
        }

        if (image.ProductTypeId is > 0)
        {
            return MapTypeDto(productId, image.ProductTypeId, null, fallbackSku);
        }

        return null;
    }

    private static string PictureIdentityKey(PictureDto picture)
    {
        if (picture.Id > 0)
        {
            return $"id:{picture.Id}";
        }

        var url = picture.PhotoUrl?.Trim() ?? string.Empty;
        var sku = picture.SkuPhoto?.Trim()
            ?? picture.ProductType?.Sku?.Trim()
            ?? string.Empty;
        return string.IsNullOrWhiteSpace(sku) ? url : $"{url}\u001f{sku}";
    }

    private static List<ProductTypeDto> DistinctTypes(
        IEnumerable<PictureDto> pictures,
        IEnumerable<ProductTypeDto>? existing = null)
    {
        var types = (existing ?? [])
            .Concat(pictures.Select(picture => picture.ProductType).Where(type => type is not null).Select(type => type!))
            .Select(type => MapTypeDto(type.ProductId, type.Id, type.Name, type.Sku, type.Price))
            .Where(type => type is not null)
            .Select(type => type!)
            .GroupBy(type => $"{type.Name}\u001f{type.Sku}", StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var type = group.First();
                type.Price = group.Max(item => item.Price);
                return type;
            })
            .ToList();
        return types;
    }

    private static ProductTypeDto? MapTypeDto(string? productId, ProductType? type) =>
        type is null ? null : MapTypeDto(productId ?? type.ProductId, type.Id, type.Name, type.Sku, type.Price);

    private static ProductTypeDto? MapTypeDto(string? productId, int? id, string? name, string? sku, decimal? price = null)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        var trimmedSku = sku?.Trim() ?? string.Empty;
        if (id is null or 0 && string.IsNullOrWhiteSpace(trimmedName) && string.IsNullOrWhiteSpace(trimmedSku))
        {
            return null;
        }

        return new ProductTypeDto
        {
            Id = id ?? 0,
            ProductId = productId,
            Name = trimmedName,
            Sku = trimmedSku,
            Price = price is > 0 ? price.Value : 0
        };
    }
}
