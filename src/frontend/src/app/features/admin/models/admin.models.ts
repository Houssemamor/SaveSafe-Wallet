export interface AdminSecuritySummary {
  totalUsers: number;
  activeUsers: number;
  suspendedUsers: number;
  deletedUsers: number;
  totalLoginEventsLast24Hours: number;
  failedLoginEventsLast24Hours: number;
  flaggedEventsLast24Hours: number;
  distinctSourceIpsLast24Hours: number;
  aiRiskScore: number;
  aiRiskLevel: 'Low' | 'Medium' | 'High' | string;
  computedAt: string;
}

export interface AdminLoginEvent {
  eventId: string;
  userId: string;
  userEmail: string;
  userName: string;
  ipAddress: string | null;
  country: string | null;
  success: boolean;
  failureReason: string | null;
  isFlagged: boolean;
  timestamp: string;
}

export interface AdminFailedLoginByIp {
  ipAddress: string;
  failedAttempts: number;
  lastAttemptAt: string;
}

export interface AdminUser {
  userId: string;
  email: string;
  name: string;
  role: string;
  accountStatus: string;
  mfaEnabled: boolean;
  createdAt: string;
  lastLoginAt: string | null;
}

export interface AdminLokiQueryRequest {
  query: string;
  hours: number;
  limit: number;
}

export interface AdminLokiQueryResponse {
  query: string;
  from: string;
  to: string;
  series: AdminLokiSeries[];
}

export interface AdminLokiSeries {
  name: string;
  labels: Record<string, string>;
  points: AdminLokiPoint[];
  total: number;
}

export interface AdminLokiPoint {
  timestamp: string;
  value: number;
}

export interface AdminAiReviewQueueResponse {
  items: AdminAiReviewItem[];
}

export interface AdminAiReviewItem {
  eventId: string;
  userId: string | null;
  email: string;
  ipAddress: string | null;
  countryCode: string | null;
  userAgent: string | null;
  success: boolean;
  failureReason: string | null;
  timestampUtc: string;
  riskScore: number;
  label: string;
  reasons: string[];
  recommendedAction: string;
  reviewStatus: string;
  analyzedAt: string;
}

export type AdminWithdrawalStatus = 'Pending' | 'Approved' | 'Rejected' | 'Failed' | string;

export interface AdminWithdrawalRequest {
  id: string;
  userId: string;
  walletId: string;
  amount: number;
  currency: string;
  status: AdminWithdrawalStatus;
  notes: string | null;
  createdAt: string;
  processedAt: string | null;
  processedBy: string | null;
  rejectionReason: string | null;
}
