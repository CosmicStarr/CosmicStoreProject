export interface IOrderItem {
  id: number;
  sku: string;
  name: string;
  status: string;
  quantity: number;
  priceAtPurchase: number;
  refundRequestedAt?: string | null;
  refundRequestReason?: string | null;
  returnTrackingNumber?: string | null;
  canRequestRefund?: boolean;
  canUpdateRefundRequest?: boolean;
}

export interface IPaymentIntent {
  paymentIntentId: string;
  clientSecret: string;
  amount: number;
  subtotal: number;
  shippingCost: number;
  currency: string;
  maskedShippingLabel?: string | null;
}

export interface IPaymentIntentRequest {
  logisticName?: string;
  shippingCost: number;
  wishlistId?: number;
  acceptedTermsVersion: string;
  acceptedTermsAt: string;
}

export interface ICjDispute {
  disputeId?: string;
  status?: string;
  disputeReason?: string;
  money?: number;
  finallyDeal?: number | null;
  finallyDealLabel?: string;
  returnReceived?: boolean;
  createDate?: string;
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
  zipCode: string;
  country: string;
  logisticName: string;
  shippingCost: number;
  trackingNumber?: string;
  returnTrackingNumber?: string;
  cjDisputeId?: string;
  cjDisputeStatus?: string;
  returnReceived?: boolean;
  requiresReturnReceipt?: boolean;
  dispute?: ICjDispute | null;
  lastStatusSyncAt?: string;
  createdAt: string;
  refundWindowEndsAt?: string;
  isWithinRefundWindow?: boolean;
  hasRefundRequest?: boolean;
  guestManageUrl?: string;
  wishlistId?: number | null;
  maskedShippingLabel?: string | null;
  acceptedTermsAt?: string | null;
  acceptedTermsVersion?: string | null;
  /** Paid but still awaiting CJ fulfillment for 20+ days (FTC 30-day buffer). */
  needsFtcShipAttention?: boolean;
  /** Whole days since order placement while still unfulfilled. */
  daysAwaitingFulfillment?: number;
  total: number;
  items: IOrderItem[];
}

export interface IGuestOrderAccess {
  orderId: string;
  token: string;
  email: string;
  zipCode: string;
}

export interface IGuestRefundRequest extends IGuestOrderAccess {
  returnTrackingNumber?: string;
  reason: string;
}

export interface ICheckoutRequest {
  email: string;
  fullName?: string;
  streetAddress?: string;
  city?: string;
  provinceOrState?: string;
  zipCode?: string;
  countryCode?: string;
  wishlistId?: number;
  stripePaymentMethodId: string;
  logisticName?: string;
  shippingCost: number;
  acceptedTermsVersion: string;
  acceptedTermsAt: string;
  items: { sku: string; amount: number; name: string; price: number; wishlistId?: number | null }[];
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
  wishlistId?: number;
  items: { sku: string; amount: number }[];
}

export interface ICjBalance {
  amount: number;
  currency: string;
  isLow: boolean;
  threshold: number;
}
