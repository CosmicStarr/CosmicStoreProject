export interface ICartItem {
  id?: number;
  name: string;
  sku: string;
  price: number;
  amount: number;
  wishlistId?: number | null;
}

export interface IShoppingCart {
  id: string;
  shoppingCartItems: ICartItem[];
  clientSecret?: string;
  paymentId?: string;
  wishlistId?: number | null;
  maskedShippingLabel?: string | null;
}

export interface IAddCartItem {
  productId: string;
  sku?: string;
  quantity: number;
  cartId?: string;
  wishlistId?: number;
}
