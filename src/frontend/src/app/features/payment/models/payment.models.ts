export interface CreateCheckoutSessionRequest {
  amount: number;
  returnBaseUrl?: string | null;
}

export interface CreateWithdrawRequest {
  amount: number;
  notes?: string | null;
}

export interface CreateCheckoutSessionResponse {
  success: boolean;
  sessionId: string | null;
  sessionUrl: string | null;
  transactionId: string | null;
  walletId: string | null;
  currency: string | null;
  errorMessage: string | null;
}

export interface CreateWithdrawResponse {
  success: boolean;
  withdrawalRequestId: string | null;
  status: string | null;
  newBalance: number | null;
  currency: string | null;
  errorMessage: string | null;
}

export interface WithdrawalRequest {
  id: string;
  amount: number;
  currency: string;
  status: 'Pending' | 'Approved' | 'Rejected' | 'Failed' | string;
  notes: string | null;
  createdAt: string;
}
