import { Component, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { AdminProductService } from '../../../core/services/admin-product';
import { AdminCjService } from '../../../core/services/admin-cj-service';
import { ICjBalance } from '../../models/order';

@Component({
  selector: 'app-admin-settings',
  imports: [CurrencyPipe],
  templateUrl: './admin-settings.html',
  styleUrl: './admin-settings.scss',
})
export class AdminSettingsComponent implements OnInit {
  private adminProducts = inject(AdminProductService);
  private adminCj = inject(AdminCjService);

  protected settings = signal<{ defaultMarkup: number } | null>(null);
  protected balance = signal<ICjBalance | null>(null);
  protected balanceError = signal<string | null>(null);
  protected syncMessage = signal<string | null>(null);
  protected syncing = signal(false);

  ngOnInit(): void {
    this.adminProducts.getSettings().subscribe({
      next: (settings) => this.settings.set(settings),
    });

    this.loadBalance();
  }

  loadBalance() {
    this.balanceError.set(null);
    this.adminCj.getBalance().subscribe({
      next: (balance) => this.balance.set(balance),
      error: (err) => this.balanceError.set(err.error?.message || 'Could not reach CJ.'),
    });
  }

  syncVariants() {
    this.runSync(
      () => this.adminCj.syncVariants(),
      (result) => `Mapped ${result.mappedVariants} CJ variant(s).`,
    );
  }

  syncStock() {
    this.runSync(
      () => this.adminCj.syncStock(),
      (result) => `Refreshed stock on ${result.updatedVariants} variant(s).`,
    );
  }

  syncOrders() {
    this.runSync(
      () => this.adminCj.syncPendingOrders(),
      (result) => `Updated ${result.updatedOrders} order(s) from CJ.`,
    );
  }

  private runSync<T>(
    request: () => import('rxjs').Observable<T>,
    describe: (result: T) => string,
  ) {
    this.syncing.set(true);
    this.syncMessage.set(null);

    request().subscribe({
      next: (result) => {
        this.syncMessage.set(describe(result));
        this.syncing.set(false);
      },
      error: (err) => {
        this.syncMessage.set(err.error?.message || 'Sync failed.');
        this.syncing.set(false);
      },
    });
  }
}
