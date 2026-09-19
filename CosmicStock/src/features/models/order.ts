export interface IOrderItem {
  sku: string;
  name: string;
  quantity: number;
  priceAtPurchase: number;
}

export interface IPaymentIntent {
  paymentIntentId: string;
  clientSecret: string;
  amount: number;
  subtotal: number;
  shippingCost: number;
  currency: string;
}

export interface IPaymentIntentRequest {
  logisticName?: string;
  shippingCost: number;
}

export interface IOrder {
  orderId: string;
  cjShipmentOrderId?: string;
  status: string;
  paymentStatus: string;
  customerName: string;
  customerEmail: string;
  shippingAddress: string;
  city: string;
  state: string;
  country: string;
  logisticName: string;
  shippingCost: number;
  trackingNumber?: string;
  lastStatusSyncAt?: string;
  createdAt: string;
  total: number;
  items: IOrderItem[];
}

export interface ICheckoutRequest {
  email: string;
  fullName: string;
  streetAddress: string;
  city: string;
  provinceOrState: string;
  countryCode: string;
  stripePaymentMethodId: string;
  logisticName?: string;
  shippingCost: number;
  items: { sku: string; amount: number; name: string; price: number }[];
}

export interface IShippingOption {
  logisticName: string;
  logisticAim?: string;
  freightCost: number;
  deliveryTime?: string;
}

export interface IShippingQuoteRequest {
  countryCode: string;
  provinceOrState?: string;
  city?: string;
  items: { sku: string; amount: number }[];
}

export interface ICjBalance {
  amount: number;
  currency: string;
  isLow: boolean;
  threshold: number;
}
