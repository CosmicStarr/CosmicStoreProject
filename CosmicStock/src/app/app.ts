import { Component, inject, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AccountService } from '../core/services/account-service';
import { CartPanelComponent } from '../core/components/cart-panel/cart-panel';



@Component({
  imports: [RouterOutlet, CartPanelComponent, ],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App implements OnInit { 
  private accountService = inject(AccountService);
  ngOnInit() {
    this.loadUser();
  }

loadUser() {
    const storedUser = localStorage.getItem('cosmicStockUser');
    if (storedUser) {
      try {
        const parsed = JSON.parse(storedUser) as { isGuest?: boolean };
        if (parsed.isGuest) {
          localStorage.removeItem('cosmicStockUser');
          return;
        }
      } catch {
        localStorage.removeItem('cosmicStockUser');
        return;
      }

      this.accountService.loadCurrentUser().subscribe({
        error: () => localStorage.removeItem('cosmicStockUser')
      });
    }
  }
}
