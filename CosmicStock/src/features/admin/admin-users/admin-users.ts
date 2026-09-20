import { Component, inject, OnInit, signal } from '@angular/core';
import { AdminUsersService, IAdminUser } from '../../../core/services/admin-users';

@Component({
  selector: 'app-admin-users',
  templateUrl: './admin-users.html',
  styleUrl: './admin-users.scss',
  host: { class: 'admin-users-page' },
})
export class AdminUsersComponent implements OnInit {
  private adminUsers = inject(AdminUsersService);

  protected users = signal<IAdminUser[]>([]);
  protected loading = signal(true);
  protected message = signal<string | null>(null);

  ngOnInit(): void {
    this.load();
  }

  load() {
    this.loading.set(true);
    this.adminUsers.getUsers().subscribe({
      next: (users) => {
        this.users.set(Array.isArray(users) ? users : []);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.message.set(err.error?.message || 'Users could not be loaded.');
      },
    });
  }
}
