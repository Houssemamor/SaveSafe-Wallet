import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../../../core/config/api.config';
import { WalletBalanceResponse, WalletHistoryResponse } from '../models/wallet.models';

@Injectable({ providedIn: 'root' })
export class WalletService {
  constructor(private readonly http: HttpClient) {}

  getBalance(): Observable<WalletBalanceResponse> {
    return this.http.get<WalletBalanceResponse>(`${API_CONFIG.walletBaseUrl}/balance`);
  }

  getHistory(pageSize: number, pageToken?: string | null): Observable<WalletHistoryResponse> {
    const params: Record<string, string | number> = { pageSize };
    if (pageToken) {
      params.pageToken = pageToken;
    }

    return this.http.get<WalletHistoryResponse>(`${API_CONFIG.walletBaseUrl}/history`, {
      params
    });
  }
}
