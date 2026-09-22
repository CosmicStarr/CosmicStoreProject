import { Component, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminProductService, IStoreRuntimeSettings } from '../../../core/services/admin-product';
import { AdminCjService } from '../../../core/services/admin-cj-service';
import { ICjBalance } from '../../models/order';

@Component({
  selector: 'app-admin-settings',
  imports: [CurrencyPipe, DatePipe, FormsModule],
  templateUrl: './admin-settings.html',
  styleUrl: './admin-settings.scss',
})
export class AdminSettingsComponent implements OnInit {
  private adminProducts = inject(AdminProductService);
  private adminCj = inject(AdminCjService);

  protected settings = signal<IStoreRuntimeSettings | null>(null);
  protected markupDraft = signal(2);
  protected syncHoursDraft = signal<6 | 12>(6);
  protected saving = signal(false);
  protected saveMessage = signal<string | null>(null);
  protected balance = signal<ICjBalance | null>(null);
  protected balanceError = signal<string | null>(null);
  protected syncMessage = signal<string | null>(null);
  protected syncing = signal(false);

  ngOnInit(): void {
    this.loadSettings();
    this.loadBalance();
  }

  loadSettings() {
    this.adminProducts.getSettings().subscribe({
      next: (settings) => {
        this.applySettings(settings);
      },
      error: (err) => {
        this.saveMessage.set(err.error?.message || 'Settings could not be loaded.');
      },
    });
  }

  loadBalance() {
    this.balanceError.set(null);
    this.adminCj.getBalance().subscribe({
      next: (balance) => this.balance.set(balance),
      error: (err) => this.balanceError.set(err.error?.message || 'Could not reach CJ.'),
    });
  }

  saveSettings() {
    const markup = Number(this.markupDraft());
    const hours = this.syncHoursDraft();
    if (!(markup > 0) || (hours !== 6 && hours !== 12)) {
      this.saveMessage.set('Markup must be greater than zero, and sync must be 6 or 12 hours.');
      return;
    }

    this.saving.set(true);
    this.saveMessage.set(null);
    this.adminProducts.updateSettings({ defaultMarkup: markup, catalogSyncHours: hours }).subscribe({
      next: (settings) => {
        this.applySettings(settings);
        this.saving.set(false);
        this.saveMessage.set('Settings saved.');
      },
      error: (err) => {
        this.saving.set(false);
        this.saveMessage.set(err.error?.message || 'Could not save settings.');
      },
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

  private applySettings(settings: IStoreRuntimeSettings) {
    this.settings.set(settings);
    this.markupDraft.set(settings.defaultMarkup);
    this.syncHoursDraft.set(settings.catalogSyncHours === 12 ? 12 : 6);
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
        this.loadSettings();
      },
      error: (err) => {
        this.syncMessage.set(err.error?.message || 'Sync failed.');
        this.syncing.set(false);
      },
    });
  }
}
