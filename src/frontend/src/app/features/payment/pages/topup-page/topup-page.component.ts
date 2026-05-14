import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { WalletService } from '../../../wallet-history/data-access/wallet.service';
import { WalletBalanceResponse } from '../../../wallet-history/models/wallet.models';
import { PaymentService } from '../../data-access/payment.service';

@Component({
  selector: 'app-topup-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './topup-page.component.html'
})
export class TopUpPageComponent implements OnInit {
  balance: WalletBalanceResponse | null = null;
  isLoadingBalance = false;
  balanceError = '';
  isSubmitting = false;
  submitError = '';

  readonly topUpForm = this.fb.nonNullable.group({
    amount: [10, [Validators.required, Validators.min(1), Validators.max(1000)]]
  });

  constructor(
    private readonly fb: FormBuilder,
    private readonly paymentService: PaymentService,
    private readonly walletService: WalletService,
    private readonly router: Router
  ) {}

  ngOnInit(): void {
    this.loadBalance();
  }

  onSubmit(): void {
    if (this.topUpForm.invalid || this.isSubmitting) {
      this.topUpForm.markAllAsTouched();
      return;
    }

    this.isSubmitting = true;
    this.submitError = '';

    const amount = Number(this.topUpForm.getRawValue().amount);
    this.paymentService.createCheckoutSession(amount, window.location.origin).subscribe({
      next: (response) => {
        this.isSubmitting = false;

        if (!response.success || !response.sessionUrl) {
          this.submitError = response.errorMessage || 'Unable to start Stripe checkout.';
          return;
        }

        window.location.href = response.sessionUrl;
      },
      error: (error: HttpErrorResponse) => {
        this.isSubmitting = false;
        const backendMessage = error.error?.errorMessage ?? error.error?.message;
        this.submitError = backendMessage || 'Unable to start Stripe checkout. Please try again.';
      }
    });
  }

  private loadBalance(): void {
    this.isLoadingBalance = true;
    this.balanceError = '';

    this.walletService.getBalance().subscribe({
      next: (balance) => {
        this.balance = balance;
        this.isLoadingBalance = false;
      },
      error: () => {
        this.balanceError = 'Unable to load wallet balance.';
        this.isLoadingBalance = false;
      }
    });
  }

  navigateToDashboard(): void {
    this.router.navigate(['/dashboard']);
  }
}
