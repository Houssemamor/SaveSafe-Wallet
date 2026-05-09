import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { publicGuard } from './core/guards/public.guard';
import { roleGuard } from './core/guards/role.guard';
import { LandingPageComponent } from './features/landing/pages/landing-page/landing-page.component';
import { LoginPageComponent } from './features/auth/pages/login-page/login-page.component';
import { RegistrationPageComponent } from './features/auth/pages/registration-page/registration-page.component';
import { DashboardPageComponent } from './features/dashboard/pages/dashboard-page/dashboard-page.component';
import { UserProfilePageComponent } from './features/profile/pages/user-profile-page/user-profile-page.component';
import { WalletHistoryPageComponent } from './features/wallet-history/pages/wallet-history-page/wallet-history-page.component';
import { AdminDashboardPageComponent } from './features/admin/pages/admin-dashboard-page/admin-dashboard-page.component';
import { TopUpPageComponent } from './features/payment/pages/topup-page/topup-page.component';
import { PaymentSuccessPageComponent } from './features/payment/pages/payment-success-page/payment-success-page.component';
import { PaymentCancelPageComponent } from './features/payment/pages/payment-cancel-page/payment-cancel-page.component';

export const routes: Routes = [
  { path: '', pathMatch: 'full', component: LandingPageComponent },
  { path: 'login', component: LoginPageComponent, canActivate: [publicGuard] },
  { path: 'register', component: RegistrationPageComponent, canActivate: [publicGuard] },
  { path: 'dashboard', component: DashboardPageComponent, canActivate: [authGuard] },
  {
    path: 'admin',
    component: AdminDashboardPageComponent,
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Admin'] }
  },
  { path: 'profile', component: UserProfilePageComponent, canActivate: [authGuard] },
  { path: 'wallet-history', component: WalletHistoryPageComponent, canActivate: [authGuard] },
  { path: 'wallet/topup', component: TopUpPageComponent, canActivate: [authGuard] },
  { path: 'wallet/success', component: PaymentSuccessPageComponent, canActivate: [authGuard] },
  { path: 'wallet/cancel', component: PaymentCancelPageComponent, canActivate: [authGuard] },
  { path: '**', redirectTo: '' }
];