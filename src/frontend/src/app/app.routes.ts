import { Routes } from '@angular/router';
import { DashboardPageComponent } from './pages/dashboard-page/dashboard-page.component';
import { LoginPageComponent } from './pages/login-page/login-page.component';
import { RegistrationPageComponent } from './pages/registration-page/registration-page.component';
import { Sprint2RequirementsPageComponent } from './pages/sprint2-requirements-page/sprint2-requirements-page.component';
import { UserProfilePageComponent } from './pages/user-profile-page/user-profile-page.component';
import { WalletHistoryPageComponent } from './pages/wallet-history-page/wallet-history-page.component';
import { authGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  { path: 'login', component: LoginPageComponent },
  { path: 'register', component: RegistrationPageComponent },
  { path: 'requirements', component: Sprint2RequirementsPageComponent },
  { path: 'dashboard', component: DashboardPageComponent, canActivate: [authGuard] },
  { path: 'profile', component: UserProfilePageComponent, canActivate: [authGuard] },
  { path: 'wallet-history', component: WalletHistoryPageComponent, canActivate: [authGuard] },
  { path: '**', redirectTo: 'login' }
];