import { IFlatCategory } from "./flattenCategory";

export interface IEditProduct {
    id: string;
    nameEn: string;
    sku: string;
    isFeatured:boolean
    isNewArrival:boolean
    isTopSelling:boolean
    sellPrice: string;
    bigImage: string;
    category: string
    productImages:IProductImage[]
}

export interface IProductImage{
    productId:string
    photoUrl:string
    skuPhoto:string
}