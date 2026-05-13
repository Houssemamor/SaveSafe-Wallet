export interface UserProfileResponse {
  userId: string;
  email: string;
  name: string;
  mfaEnabled: boolean;
  accountStatus: string;
  role: string;
  createdAt: string;
  lastLoginAt: string | null;
  profilePictureUrl?: string | null;
  hasPassword: boolean;
  isGoogleAccount: boolean;
}

export interface UpdateUserProfileRequest {
  name: string;
}

export interface UpdatePasswordRequest {
  newPassword: string;
  currentPassword?: string;
}
