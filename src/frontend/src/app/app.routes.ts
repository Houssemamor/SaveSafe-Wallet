import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';
import { LoginPageComponent } from './features/auth/pages/login-page/login-page.component';
import { RegistrationPageComponent } from './features/auth/pages/registration-page/registration-page.component';
import { DashboardPageComponent } from './features/dashboard/pages/dashboard-page/dashboard-page.component';
import { UserProfilePageComponent } from './features/profile/pages/user-profile-page/user-profile-page.component';
import { WalletHistoryPageComponent } from './features/wallet-history/pages/wallet-history-page/wallet-history-page.component';
import { AdminDashboardPageComponent } from './features/admin/pages/admin-dashboard-page/admin-dashboard-page.component';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  { path: 'login', component: LoginPageComponent },
  { path: 'register', component: RegistrationPageComponent },
  { path: 'dashboard', component: DashboardPageComponent, canActivate: [authGuard] },
  {
    path: 'admin',
    component: AdminDashboardPageComponent,
    canActivate: [authGuard, roleGuard],
    data: { roles: ['Admin'] }
  },
  { path: 'profile', component: UserProfilePageComponent, canActivate: [authGuard] },
  { path: 'wallet-history', component: WalletHistoryPageComponent, canActivate: [authGuard] },
  { path: '**', redirectTo: 'login' }
];