import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../../../core/config/api.config';
import { CreateCheckoutSessionRequest, CreateCheckoutSessionResponse } from '../models/payment.models';

@Injectable({ providedIn: 'root' })
export class PaymentService {
  constructor(private readonly http: HttpClient) {}

  createCheckoutSession(amount: number): Observable<CreateCheckoutSessionResponse> {
    const request: CreateCheckoutSessionRequest = { amount };
    return this.http.post<CreateCheckoutSessionResponse>(`${API_CONFIG.paymentBaseUrl}/create-checkout-session`, request);
  }
}
