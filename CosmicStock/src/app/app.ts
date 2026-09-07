import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AccountService } from '../core/services/account-service';


@Component({
  imports: [RouterOutlet],
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
      // Validates token against the backend [HttpGet("account")] endpoint
      this.accountService.loadCurrentUser().subscribe({
        error: () => localStorage.removeItem('cosmicStockUser') // Clears storage if token expired
      });
    }
  }
}
