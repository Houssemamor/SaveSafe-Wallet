import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { WalletService } from '../../../wallet-history/data-access/wallet.service';
import { WalletBalanceResponse } from '../../../wallet-history/models/wallet.models';

@Component({
  selector: 'app-payment-success-page',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './payment-success-page.component.html'
})
export class PaymentSuccessPageComponent implements OnInit {
  balance: WalletBalanceResponse | null = null;
  isLoading = false;
  error = '';

  constructor(private readonly walletService: WalletService) {}

  ngOnInit(): void {
    this.isLoading = true;
    this.walletService.getBalance().subscribe({
      next: (balance) => {
        this.balance = balance;
        this.isLoading = false;
      },
      error: () => {
        this.error = 'Paiement reçu, mais impossible de recharger le solde pour le moment.';
        this.isLoading = false;
      }
    });
  }
}
