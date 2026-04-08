export interface UserProfileResponse {
  userId: string;
  email: string;
  name: string;
  mfaEnabled: boolean;
  accountStatus: string;
  role: string;
  createdAt: string;
  lastLoginAt: string | null;
}

export interface UpdateUserProfileRequest {
  name: string;
}
