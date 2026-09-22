export interface IUser {
  email: string;
  token: string;
  userName: string;
  emailConfirmed: boolean;
  isGuest?: boolean;
  pendingEmail?: string | null;
  confirmationEmailSent?: boolean;
}

export interface ILoginValues {
  email: string;
  password: string;
}

export interface IRegisterValues extends ILoginValues {
  userName: string;
  confirmPassword: string;
  acceptedTermsVersion: string;
  acceptedTermsAt: string;
}

export interface IUserAddress {
  id: number;
  label: string;
  fullName: string;
  streetAddress: string;
  city: string;
  provinceOrState: string;
  zipCode: string;
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

export interface IWishlist {
  id: number;
  publicId: string;
  name: string;
  shippingAddressId?: number | null;
  isGiftRegistry: boolean;
  isAddressPrivate: boolean;
  maskedShippingLabel?: string | null;
  shareUrl?: string | null;
  fulfillmentDisclaimer: string;
  items: IWishlistItem[];
}

export interface IPublicWishlist {
  id: number;
  publicId: string;
  name: string;
  maskedShippingLabel: string;
  fulfillmentDisclaimer: string;
  items: IWishlistItem[];
}
