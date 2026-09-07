import { IPicture } from "./pictureResponse";

export interface IProductResponse {
  id: string;
  nameEn: string;
  sku: string;
  descriptionEn: string;
  isFeatured: boolean;
  isNewArrival: boolean;
  isTopSelling: boolean;
  sellPrice: number;
  bigImage: string;
  category: string;
  pictures: IPicture[]; // The one-to-many relationship

}