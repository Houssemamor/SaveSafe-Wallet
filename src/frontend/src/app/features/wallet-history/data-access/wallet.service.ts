import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { API_CONFIG } from '../../../core/config/api.config';
import { WalletBalanceResponse, WalletHistoryResponse } from '../models/wallet.models';

/**
 * Wallet interface representing a user's wallet account
 */
export interface Wallet {
  id: string;
  name: string;
  type: 'checking' | 'savings' | 'investment' | 'reserve';
  balance: number;
  currency: string;
  isActive: boolean;
  createdAt: string;
  isDefault: boolean;
}

/**
 * Request interface for creating a new wallet
 */
export interface CreateWalletRequest {
  name: string;
  type: 'checking' | 'savings' | 'investment' | 'reserve';
  currency: string;
  initialBalance?: number;
}

/**
 * Response interface for wallet creation
 */
export interface CreateWalletResponse {
  success: boolean;
  wallet?: Wallet;
  errorMessage?: string;
}

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

  transferBetweenWallets(sourceWalletId: string, targetWalletId: string, amount: number): Observable<any> {
    return this.http.post(`${API_CONFIG.walletBaseUrl}/internal-transfer`, {
      sourceWalletId,
      targetWalletId,
      amount
    });
  }

  /**
   * Get all wallets for the current user
   * @returns Observable containing array of user wallets
   */
  getWallets(): Observable<Wallet[]> {
    return this.http.get<Wallet[]>(`${API_CONFIG.walletBaseUrl}/wallets`);
  }

  /**
   * Create a new wallet for the user
   * @param request - Wallet creation request with name, type, and currency
   * @returns Observable containing creation response
   */
  createWallet(request: CreateWalletRequest): Observable<CreateWalletResponse> {
    return this.http.post<CreateWalletResponse>(`${API_CONFIG.walletBaseUrl}/wallets`, request);
  }

  /**
   * Delete a wallet by ID
   * @param walletId - ID of wallet to delete
   * @returns Observable of void
   */
  deleteWallet(walletId: string): Observable<void> {
    return this.http.delete<void>(`${API_CONFIG.walletBaseUrl}/wallets/${walletId}`, {
      observe: 'response'
    }).pipe(
      map(response => {
        if (response.status === 204) {
          return undefined;
        }
        throw new Error('Unexpected response status');
      }),
      catchError(error => {
        // Extract error message from backend response if available
        const errorMessage = error.error?.message || error.error || 'Failed to delete wallet';
        throw new Error(typeof errorMessage === 'string' ? errorMessage : 'Failed to delete wallet');
      })
    );
  }

  /**
   * Set a wallet as the default wallet
   * @param walletId - ID of wallet to set as default
   * @returns Observable of void
   */
  setDefaultWallet(walletId: string): Observable<void> {
    return this.http.post<void>(`${API_CONFIG.walletBaseUrl}/wallets/${walletId}/set-default`, {});
  }

  /**
   * Get balance for a specific wallet
   * @param walletId - ID of wallet to get balance for
   * @returns Observable containing wallet balance response
   */
  getWalletBalance(walletId: string): Observable<WalletBalanceResponse> {
    return this.http.get<WalletBalanceResponse>(`${API_CONFIG.walletBaseUrl}/wallets/${walletId}/balance`);
  }
}
