import { Component, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { OrderService } from '../../../core/services/order-service';
import { IGuestOrderAccess, IOrder, IOrderItem } from '../../models/order';
import { SiteNavbarComponent } from '../../../core/components/site-navbar/site-navbar';
import { SiteFooterComponent } from '../../../core/components/site-footer/site-footer';

@Component({
  selector: 'app-guest-order-manage',
  imports: [CurrencyPipe, DatePipe, RouterLink, SiteNavbarComponent, SiteFooterComponent],
  templateUrl: './guest-order-manage.html',
  styleUrl: './guest-order-manage.scss',
})
export class GuestOrderManageComponent implements OnInit {
  private orderService = inject(OrderService);
  private route = inject(ActivatedRoute);

  protected order = signal<IOrder | null>(null);
  protected verifying = signal(false);
  protected cancelling = signal(false);
  protected busyItemId = signal<number | null>(null);
  protected refundItemId = signal<number | null>(null);
  protected returnTrackingDraft = signal('');
  protected refundReasonDraft = signal('');
  protected message = signal<string | null>(null);
  protected messageIsError = signal(false);
  protected emailDraft = signal('');
  protected zipDraft = signal('');
  protected orderId = signal('');
  protected token = signal('');

  ngOnInit(): void {
    const params = this.route.snapshot.queryParamMap;
    this.orderId.set(params.get('orderId')?.trim() ?? '');
    this.token.set(params.get('token')?.trim() ?? '');
  }

  hasManageLink() {
    return !!this.orderId() && !!this.token();
  }

  onEmailInput(event: Event) {
    this.emailDraft.set((event.target as HTMLInputElement).value);
  }

  onZipInput(event: Event) {
    this.zipDraft.set((event.target as HTMLInputElement).value);
  }

  access(): IGuestOrderAccess {
    return {
      orderId: this.orderId(),
      token: this.token(),
      email: this.emailDraft().trim(),
      zipCode: this.zipDraft().trim(),
    };
  }

  verify() {
    if (!this.hasManageLink()) {
      this.messageIsError.set(true);
      this.message.set('Open the manage link from your confirmation email, then confirm with the email and ZIP used at checkout.');
      return;
    }

    const access = this.access();
    if (!access.email) {
      this.messageIsError.set(true);
      this.message.set('Enter the email used at checkout. Include the ZIP for a standard order; gift registry purchases do not need it.');
      return;
    }

    this.verifying.set(true);
    this.message.set(null);
    this.orderService.verifyGuestOrder(access).subscribe({
      next: (order) => {
        this.order.set(order);
        this.verifying.set(false);
        this.messageIsError.set(false);
        this.message.set(null);
      },
      error: (err) => {
        this.verifying.set(false);
        this.order.set(null);
        this.messageIsError.set(true);
        this.message.set(this.apiMessage(err, 'We could not verify this order. Check the confirmation email, ZIP, and try again.'));
      },
    });
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
    this.orderService.requestGuestItemRefund(item.id, {
      ...this.access(),
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
          this.apiMessage(err, 'That refund request could not be submitted. Please try again or contact support.'),
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
    this.orderService.cancelGuestOrder(this.access()).subscribe({
      next: (updated) => {
        this.order.set(updated);
        this.cancelling.set(false);
        this.messageIsError.set(false);
        this.message.set('Your order was cancelled. We emailed a confirmation with the refunded amount. Refunds typically appear in 3-5 business days.');
      },
      error: (err) => {
        this.cancelling.set(false);
        this.messageIsError.set(true);
        this.message.set(
          this.apiMessage(err, 'This order could not be cancelled. Please try again or contact support.'),
        );
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
    this.orderService.cancelGuestOrderItem(item.id, this.access()).subscribe({
      next: (updated) => {
        this.order.set(updated);
        this.busyItemId.set(null);
        this.messageIsError.set(false);
        const remaining = updated.items.filter(
          (line) => line.status !== 'Cancelled' && line.status !== 'Refunded',
        );
        this.message.set(
          remaining.length === 0
            ? `${label} was cancelled. This order is now fully cancelled and refunded. We emailed a confirmation; refunds typically appear in 3-5 business days.`
            : `${label} was cancelled. $${amount} will return to your original payment method. Other items stay on the order.`,
        );
      },
      error: (err) => {
        this.busyItemId.set(null);
        this.messageIsError.set(true);
        this.message.set(
          this.apiMessage(err, 'That item could not be cancelled. Please try again or contact support.'),
        );
      },
    });
  }

  private apiMessage(err: { status?: number; error?: { message?: string } }, fallback: string) {
    const message = err.error?.message?.trim();
    if (err.status && err.status >= 400 && message) {
      return message;
    }
    return fallback;
  }
}
