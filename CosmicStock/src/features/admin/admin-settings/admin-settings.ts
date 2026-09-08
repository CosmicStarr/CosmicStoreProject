import { Component, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { AdminProductService } from '../../../core/services/admin-product';

@Component({
  selector: 'app-admin-settings',
  imports: [CurrencyPipe],
  templateUrl: './admin-settings.html',
  styleUrl: './admin-settings.scss',
})
export class AdminSettingsComponent implements OnInit {
  private adminProducts = inject(AdminProductService);
  protected settings = signal<{ defaultMarkup: number } | null>(null);

  ngOnInit(): void {
    this.adminProducts.getSettings().subscribe({
      next: (settings) => this.settings.set(settings),
    });
  }
}
