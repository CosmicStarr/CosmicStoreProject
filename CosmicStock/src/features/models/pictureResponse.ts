export interface IPicture {
  id?: number;
  productId?: string;
  photoUrl: string;
  skuPhoto?: string;
  productTypeId?: number;
  productType?: IProductType;
  /** Older API/cache payloads used a string Type on each picture. */
  type?: string;
  /** Synced from CJ but not saved to the storefront yet. */
  isStorefrontDraft?: boolean;
}

export interface IProductType {
  id?: number;
  productId?: string;
  name: string;
  sku: string;
  price?: number;
}
