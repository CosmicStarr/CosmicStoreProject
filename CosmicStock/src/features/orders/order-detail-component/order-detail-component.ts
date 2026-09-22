import { Component, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { OrderService } from '../../../core/services/order-service';
import { IOrder, IOrderItem } from '../../models/order';
import { SiteNavbarComponent } from '../../../core/components/site-navbar/site-navbar';

@Component({
  selector: 'app-order-detail-component',
  imports: [CurrencyPipe, DatePipe, RouterLink, SiteNavbarComponent],
  templateUrl: './order-detail-component.html',
  styleUrl: './order-detail-component.scss',
})
export class OrderDetailComponent implements OnInit {
  private orderService = inject(OrderService);
  private route = inject(ActivatedRoute);

  protected order = signal<IOrder | null>(null);
  protected loading = signal(true);
  protected cancelling = signal(false);
  protected busyItemId = signal<number | null>(null);
  protected refundItemId = signal<number | null>(null);
  protected returnTrackingDraft = signal('');
  protected refundReasonDraft = signal('');
  protected message = signal<string | null>(null);
  protected messageIsError = signal(false);

  ngOnInit(): void {
    this.load();
  }

  lineTotal(item: IOrderItem) {
    return item.priceAtPurchase * item.quantity;
  }

  canCancel() {
    const current = this.order();
    if (!current) return false;
    return !['Shipped', 'Delivered', 'Cancelled', 'Refunded'].includes(current.status);
  }

  canCancelItem(item: IOrderItem) {
    if (!this.canCancel()) return false;
    return item.status !== 'Cancelled' && item.status !== 'Refunded' && item.status !== 'RefundRequested';
  }

  isRefundRequested(item: IOrderItem) {
    return item.status === 'RefundRequested' || !!item.refundRequestedAt || !!item.returnTrackingNumber;
  }

  canRequestRefund(item: IOrderItem) {
    return !!item.canRequestRefund;
  }

  canUpdateRefundRequest(item: IOrderItem) {
    return !!item.canUpdateRefundRequest;
  }

  showRefundHelp() {
    const current = this.order();
    if (!current) return false;
    return !!current.requiresReturnReceipt
      || !!current.hasRefundRequest
      || (current.items ?? []).some((item) => item.canRequestRefund || item.canUpdateRefundRequest);
  }

  refundWindowEnded() {
    const current = this.order();
    if (!current?.requiresReturnReceipt) return false;
    return current.isWithinRefundWindow === false;
  }

  startRefundRequest(item: IOrderItem) {
    this.refundItemId.set(item.id);
    this.returnTrackingDraft.set(item.returnTrackingNumber?.trim() ?? '');
    this.refundReasonDraft.set(item.refundRequestReason?.trim() ?? '');
    this.message.set(null);
  }

  cancelRefundForm() {
    this.refundItemId.set(null);
    this.returnTrackingDraft.set('');
    this.refundReasonDraft.set('');
  }

  onReturnTrackingInput(event: Event) {
    this.returnTrackingDraft.set((event.target as HTMLInputElement).value);
  }

  onRefundReasonInput(event: Event) {
    this.refundReasonDraft.set((event.target as HTMLTextAreaElement).value);
  }

  submitRefundRequest(item: IOrderItem) {
    const current = this.order();
    if (!current) return;

    const reason = this.refundReasonDraft().trim();
    if (reason.length < 10) {
      this.messageIsError.set(true);
      this.message.set('Describe the problem in at least a few words (damage, wrong item, missing parts, or a lost package). Change of mind is not refundable after shipment.');
      return;
    }

    const tracking = this.returnTrackingDraft().trim();
    this.busyItemId.set(item.id);
    this.message.set(null);
    this.orderService.requestItemRefund(current.orderId, item.id, {
      returnTrackingNumber: tracking || undefined,
      reason,
    }).subscribe({
      next: (updated) => {
        this.order.set(updated);
        this.busyItemId.set(null);
        this.refundItemId.set(null);
        this.messageIsError.set(false);
        this.message.set(
          `Refund requested for ${item.name || item.sku}. We will email you if we need photos, video, or a post-office certificate.`,
        );
      },
      error: (err) => {
        this.busyItemId.set(null);
        this.messageIsError.set(true);
        this.message.set(
          err.error?.message || 'That refund request could not be submitted. Please try again or contact support.',
        );
      },
    });
  }

  requestCancellation() {
    const current = this.order();
    if (!current) return;

    const confirmed = window.confirm(
      'Cancel this entire order? If it has not shipped yet, we will stop fulfillment and refund your payment.',
    );
    if (!confirmed) return;

    this.cancelling.set(true);
    this.message.set(null);
    this.orderService.requestCancellation(current.orderId).subscribe({
      next: (updated) => {
        this.order.set(updated);
        this.cancelling.set(false);
        this.messageIsError.set(false);
        this.message.set('Your order was cancelled. Any refund will return to the original payment method.');
      },
      error: (err) => {
        this.cancelling.set(false);
        this.messageIsError.set(true);
        this.message.set(
          err.error?.message || 'This order could not be cancelled. Please try again or contact support.',
        );
        this.load();
      },
    });
  }

  cancelItem(item: IOrderItem) {
    const current = this.order();
    if (!current) return;

    const amount = this.lineTotal(item).toFixed(2);
    const label = item.name || item.sku;
    const confirmed = window.confirm(
      `Cancel ${label} and refund $${amount}? Other items on this order will still ship.`,
    );
    if (!confirmed) return;

    this.busyItemId.set(item.id);
    this.message.set(null);
    this.orderService.cancelOrderItem(current.orderId, item.id).subscribe({
      next: (updated) => {
        this.order.set(updated);
        this.busyItemId.set(null);
        this.messageIsError.set(false);
        const remaining = updated.items.filter(
          (line) => line.status !== 'Cancelled' && line.status !== 'Refunded',
        );
        this.message.set(
          remaining.length === 0
            ? `${label} was cancelled. This order is now fully cancelled and refunded.`
            : `${label} was cancelled. $${amount} will return to your original payment method. Other items stay on the order.`,
        );
      },
      error: (err) => {
        this.busyItemId.set(null);
        this.messageIsError.set(true);
        this.message.set(
          err.error?.message || 'That item could not be cancelled. Please try again or contact support.',
        );
        this.load();
      },
    });
  }

  private load() {
    const orderId = this.route.snapshot.paramMap.get('id');
    if (!orderId) {
      this.loading.set(false);
      return;
    }

    this.loading.set(true);
    this.orderService.getOrder(orderId).subscribe({
      next: (order) => {
        this.order.set(order);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }
}
