export interface IOrderItem {
  sku: string;
  name: string;
  quantity: number;
  priceAtPurchase: number;
}

export interface IOrder {
  orderId: string;
  cjShipmentOrderId?: string;
  status: string;
  customerName: string;
  customerEmail: string;
  shippingAddress: string;
  city: string;
  state: string;
  country: string;
  createdAt: string;
  total: number;
  items: IOrderItem[];
}

export interface ICheckoutRequest {
  fullName: string;
  streetAddress: string;
  city: string;
  provinceOrState: string;
  countryCode: string;
  stripePaymentMethodId: string;
  items: { sku: string; amount: number; name: string; price: number }[];
}
