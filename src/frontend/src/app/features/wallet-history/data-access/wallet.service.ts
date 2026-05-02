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
      params['pageToken'] = pageToken;
    }

    return this.http.get<WalletHistoryResponse>(`${API_CONFIG.walletBaseUrl}/history`, {
      params
    });
  }

  transferFunds(recipientEmail: string, amount: number, description?: string): Observable<any> {
    return this.http.post(`${API_CONFIG.walletBaseUrl}/transfer`, {
      recipientEmail,
      amount,
      description
    });
  }

  exportHistory(startDate?: string, endDate?: string): Observable<Blob> {
    const params: Record<string, string> = {};
    if (startDate) params['startDate'] = startDate;
    if (endDate) params['endDate'] = endDate;

    return this.http.get(`${API_CONFIG.walletBaseUrl}/export`, {
      params,
      responseType: 'blob'
    });
  }
}
