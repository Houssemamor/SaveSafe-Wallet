export interface AdminSecuritySummary {
  totalUsers: number;
  activeUsers: number;
  suspendedUsers: number;
  deletedUsers: number;
  totalLoginEventsLast24Hours: number;
  failedLoginEventsLast24Hours: number;
  flaggedEventsLast24Hours: number;
  distinctSourceIpsLast24Hours: number;
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
