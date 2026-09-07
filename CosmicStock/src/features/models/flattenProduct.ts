import { IFlatCategory } from "./flattenCategory";

export interface IFlatProduct {
    id: string;
    nameEn: string;
    sku: string;
    sellPrice: number;
    bigImage: string;
    category: IFlatCategory
}