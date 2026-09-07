import { Routes } from '@angular/router';
import { HomeComponent } from '../features/home/home-component/home-component';
import { authGuard } from '../core/guards/auth-guard';

export const routes: Routes = [
  { path: '', component: HomeComponent },
  { path: 'admin', loadComponent: () => import('../features/admin/admin-layout/admin-layout').then(m => m.AdminLayoutComponent) },
  { path: 'admin/dashboard', loadComponent: () => import('../features/admin/dashboard/dashboard').then(m => m.DashboardComponent) },
  { path: 'admin/edit-Product/:id', loadComponent: () => import('../features/admin/edit-product/edit-product').then(m => m.EditProductComponent) },
  { path: 'store', loadComponent: () => import('../features/store/products-component').then(m => m.ProductsComponent) },
  { path: 'store/:id', loadComponent: () => import('../features/store/products-card-component/products-card-component').then(m => m.ProductDetailComponent) },
  { path: 'cart', loadComponent: () => import('../features/cart/cart-component/cart-component').then(m => m.CartComponent) },
  { path: 'checkout', loadComponent: () => import('../features/checkout/checkout-component/checkout-component').then(m => m.CheckoutComponent), canActivate: [authGuard] },
  { path: 'orders', loadComponent: () => import('../features/orders/orders-component/orders-component').then(m => m.OrdersComponent), canActivate: [authGuard] },
  { path: 'orders/:id', loadComponent: () => import('../features/orders/order-detail-component/order-detail-component').then(m => m.OrderDetailComponent), canActivate: [authGuard] },
  { path: 'register', loadComponent: () => import('../features/users/register-component/register-component').then(m => m.RegisterComponent) },
  { path: 'login', loadComponent: () => import('../features/users/login-component/login-component').then(m => m.LoginComponent) },
];
