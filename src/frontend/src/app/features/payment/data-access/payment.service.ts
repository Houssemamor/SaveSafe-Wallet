import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../../../core/config/api.config';
import {
  CreateCheckoutSessionRequest,
  CreateCheckoutSessionResponse,
  CreateWithdrawRequest,
  CreateWithdrawResponse,
  WithdrawalRequest
} from '../models/payment.models';

@Injectable({ providedIn: 'root' })
export class PaymentService {
  constructor(private readonly http: HttpClient) {}

  createCheckoutSession(amount: number, returnBaseUrl?: string): Observable<CreateCheckoutSessionResponse> {
    const request: CreateCheckoutSessionRequest = { amount, returnBaseUrl: returnBaseUrl?.trim() || null };
    return this.http.post<CreateCheckoutSessionResponse>(`${API_CONFIG.paymentBaseUrl}/create-checkout-session`, request);
  }

  createWithdrawRequest(amount: number, notes?: string): Observable<CreateWithdrawResponse> {
    const request: CreateWithdrawRequest = {
      amount,
      notes: notes?.trim() ? notes.trim() : null
    };

    return this.http.post<CreateWithdrawResponse>(`${API_CONFIG.paymentBaseUrl}/withdraw`, request);
  }

  getWithdrawals(): Observable<WithdrawalRequest[]> {
    return this.http.get<WithdrawalRequest[]>(`${API_CONFIG.paymentBaseUrl}/withdrawals`);
  }
}
