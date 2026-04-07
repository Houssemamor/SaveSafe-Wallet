import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { SessionUser } from '../../models/auth.models';
import { WalletBalanceResponse } from '../../models/wallet.models';
import { AuthService } from '../../services/auth.service';
import { SessionService } from '../../services/session.service';
import { WalletService } from '../../services/wallet.service';

@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [RouterLink, CommonModule],
  templateUrl: './dashboard-page.component.html'
})
export class DashboardPageComponent implements OnInit {
  user: SessionUser | null = null;
  balance: WalletBalanceResponse | null = null;
  balanceError = '';

  get liquidityIntegerPart(): string {
    const amount = this.balance?.balance ?? 0;
    return Math.trunc(amount).toLocaleString('en-US');
  }

  get liquidityDecimalPart(): string {
    const amount = this.balance?.balance ?? 0;
    const decimals = Math.round((amount - Math.trunc(amount)) * 100);
    return decimals.toString().padStart(2, '0');
  }

  constructor(
    private readonly sessionService: SessionService,
    private readonly walletService: WalletService,
    private readonly authService: AuthService,
    private readonly router: Router
  ) {}

  ngOnInit(): void {
    this.user = this.sessionService.currentUser;
    this.loadBalance();
  }

  logout(): void {
    this.authService.logout().subscribe({
      next: () => this.router.navigate(['/login']),
      error: () => {
        this.authService.clearLocalSession();
        this.router.navigate(['/login']);
      }
    });
  }

  private loadBalance(): void {
    this.walletService.getBalance().subscribe({
      next: (balance) => {
        this.balance = balance;
        this.balanceError = '';
      },
      error: () => {
        this.balanceError = 'Unable to load wallet balance.';
      }
    });
  }
}