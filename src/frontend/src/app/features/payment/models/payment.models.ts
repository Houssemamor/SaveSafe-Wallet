export interface CreateCheckoutSessionRequest {
  amount: number;
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
