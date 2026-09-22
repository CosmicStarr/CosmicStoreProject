import { IPicture, IProductType } from "./pictureResponse";

export interface IProductResponse {
  id: string;
  nameEn: string;
  sku: string;
  descriptionEn: string;
  shortDescription?: string;
  isFeatured: boolean;
  isNewArrival: boolean;
  isTopSelling: boolean;
  sellPrice: number;
  stockQuantity: number;
  bigImage: string;
  category: string;
  types?: IProductType[];
  pictures: IPicture[];
}

export interface ICategorySummary {
  name: string;
  count: number;
}
