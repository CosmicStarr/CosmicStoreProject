export interface IUser {
  email: string;
  token: string;
  userName: string;
}

export interface ILoginValues {
  email: string;
  password: string;
}

export interface IRegisterValues extends ILoginValues {
  userName: string;
  confirmPassword: string;
}

export interface IUserAddress {
  id: number;
  label: string;
  fullName: string;
  streetAddress: string;
  city: string;
  provinceOrState: string;
  countryCode: string;
  isDefault: boolean;
}

export interface IWishlistItem {
  id: number;
  productId: string;
  nameEn: string;
  sku: string;
  sellPrice: number;
  bigImage?: string;
  addedAt: string;
}
