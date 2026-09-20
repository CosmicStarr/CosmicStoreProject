export interface IEditProduct {
    id: string;
    nameEn: string;
    sku: string;
    descriptionEn?: string;
    shortDescription?: string;
    isFeatured: boolean;
    isNewArrival: boolean;
    isTopSelling: boolean;
    sellPrice: string;
    stockQuantity?: number;
    bigImage: string;
    category: string;
    productImages: IProductImage[];
    cjProductId?: string;
    variants?: ICjVariant[];
}

export interface IProductImage {
    id?: number;
    productId: string;
    photoUrl: string;
    skuPhoto: string;
    type?: string;
}

export interface ICjVariant {
    vid: string;
    sku: string;
    variantName?: string;
    sellPrice: number;
    stockQuantity?: number;
    imageUrl?: string;
}

export interface ICjProductImport {
    cjProductId: string;
    nameEn: string;
    sku: string;
    descriptionEn?: string;
    shortDescription?: string;
    bigImage?: string;
    category?: string;
    sellPrice: number;
    variants: ICjVariant[];
}