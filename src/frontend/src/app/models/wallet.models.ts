export interface WalletBalanceResponse {
  accountId: string;
  accountNumber: string;
  currency: string;
  balance: number;
  updatedAt: string;
}

export interface WalletHistoryEntry {
  id: string;
  type: string;
  amount: number;
  balanceAfter: number;
  description: string | null;
  createdAt: string;
}

export interface WalletHistoryResponse {
  entries: WalletHistoryEntry[];
  page: number;
  pageSize: number;
  totalCount: number;
}
