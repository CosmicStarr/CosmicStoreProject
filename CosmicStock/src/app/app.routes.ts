import { Routes } from '@angular/router';
import { HomeComponent } from '../features/home/home-component/home-component';
import { authGuard } from '../core/guards/auth-guard';
import { adminGuard } from '../core/guards/admin-guard';
import { guestGuard } from '../core/guards/guest-guard';
import { registeredAccountGuard } from '../core/guards/registered-account-guard';

export const routes: Routes = [
  { path: '', component: HomeComponent },
  {
    path: 'admin',
    loadComponent: () => import('../features/admin/admin-layout/admin-layout').then(m => m.AdminLayoutComponent),
    canActivate: [authGuard, adminGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', loadComponent: () => import('../features/admin/dashboard/dashboard').then(m => m.DashboardComponent) },
      { path: 'add-product', loadComponent: () => import('../features/admin/edit-product/edit-product').then(m => m.EditProductComponent), data: { mode: 'create' } },
      { path: 'edit-Product/:id', loadComponent: () => import('../features/admin/edit-product/edit-product').then(m => m.EditProductComponent) },
      { path: 'orders', loadComponent: () => import('../features/admin/admin-orders/admin-orders').then(m => m.AdminOrdersComponent) },
      { path: 'orders/:orderId', loadComponent: () => import('../features/admin/admin-order-detail/admin-order-detail').then(m => m.AdminOrderDetailComponent) },
      { path: 'users', loadComponent: () => import('../features/admin/admin-users/admin-users').then(m => m.AdminUsersComponent) },
      { path: 'settings', loadComponent: () => import('../features/admin/admin-settings/admin-settings').then(m => m.AdminSettingsComponent) },
    ],
  },
  { path: 'store', loadComponent: () => import('../features/store/products-component').then(m => m.ProductsComponent) },
  { path: 'store/:id', loadComponent: () => import('../features/store/products-card-component/products-card-component').then(m => m.ProductDetailComponent) },
  { path: 'cart', loadComponent: () => import('../features/cart/cart-component/cart-component').then(m => m.CartComponent) },
  { path: 'checkout/confirmation', loadComponent: () => import('../features/checkout/order-confirmation/order-confirmation').then(m => m.OrderConfirmationComponent) },
  { path: 'checkout', loadComponent: () => import('../features/checkout/checkout-component/checkout-component').then(m => m.CheckoutComponent) },
  { path: 'orders', loadComponent: () => import('../features/orders/orders-component/orders-component').then(m => m.OrdersComponent), canActivate: [authGuard] },
  { path: 'orders/:id', loadComponent: () => import('../features/orders/order-detail-component/order-detail-component').then(m => m.OrderDetailComponent), canActivate: [authGuard] },
  { path: 'account/confirm-email', loadComponent: () => import('../features/account/confirm-email/confirm-email').then(m => m.ConfirmEmailComponent) },
  { path: 'account/confirm-email-change', loadComponent: () => import('../features/account/confirm-email-change/confirm-email-change').then(m => m.ConfirmEmailChangeComponent) },
  { path: 'account/lock-account', loadComponent: () => import('../features/account/lock-account/lock-account').then(m => m.LockAccountComponent) },
  { path: 'account/forgot-password', loadComponent: () => import('../features/account/forgot-password/forgot-password').then(m => m.ForgotPasswordComponent), canActivate: [guestGuard] },
  { path: 'account/reset-password', loadComponent: () => import('../features/account/reset-password/reset-password').then(m => m.ResetPasswordComponent) },
  { path: 'account/profile', loadComponent: () => import('../features/account/profile/profile').then(m => m.ProfileComponent), canActivate: [authGuard, registeredAccountGuard] },
  { path: 'account/wishlist', loadComponent: () => import('../features/account/wishlist/wishlist').then(m => m.WishlistComponent), canActivate: [authGuard] },
  { path: 'register', loadComponent: () => import('../features/users/register-component/register-component').then(m => m.RegisterComponent), canActivate: [guestGuard] },
  { path: 'login', loadComponent: () => import('../features/users/login-component/login-component').then(m => m.LoginComponent), canActivate: [guestGuard] },
  { path: '**', loadComponent: () => import('../features/not-found/not-found').then(m => m.NotFoundComponent) },
];
