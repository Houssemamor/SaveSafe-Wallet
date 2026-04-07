import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../config/api.config';
import { WalletBalanceResponse } from '../models/wallet.models';

@Injectable({ providedIn: 'root' })
export class WalletService {
  constructor(private readonly http: HttpClient) {}

  getBalance(): Observable<WalletBalanceResponse> {
    return this.http.get<WalletBalanceResponse>(`${API_CONFIG.walletBaseUrl}/balance`);
  }
}
