export interface ICartItem {
  id?: number;
  name: string;
  sku: string;
  price: number;
  amount: number;
}

export interface IShoppingCart {
  id: string;
  shoppingCartItems: ICartItem[];
  clientSecret?: string;
  paymentId?: string;
}

export interface IAddCartItem {
  productId: string;
  sku?: string;
  quantity: number;
  cartId?: string;
}
