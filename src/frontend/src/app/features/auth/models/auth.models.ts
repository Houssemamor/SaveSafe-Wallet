export interface LoginRequest {
  email: string;
  password: string;
}

export interface MfaEnrollQuestionRequest {
  questionId: string;
  answer: string;
}

export interface MfaEnrollRequest {
  questions: MfaEnrollQuestionRequest[];
}

export interface MfaEnrollResponse {
  success: boolean;
  mfaEnabled: boolean;
  questionCount: number;
}

export interface MfaDisableResponse {
  success: boolean;
  mfaEnabled: boolean;
}

export interface MfaVerifyRequest {
  challengeToken: string;
  answer: string;
}

export interface SecurityQuestionCatalogItem {
  questionId: string;
  questionText: string;
}

export interface SecurityQuestionCatalogResponse {
  questions: SecurityQuestionCatalogItem[];
}

export interface RegisterRequest {
  email: string;
  name: string;
  password: string;
}

export interface AuthResponse {
  accessToken?: string;
  tokenType?: string;
  expiresIn?: number;
  userId?: string;
  email?: string;
  name?: string;
  role?: string;
  profilePictureUrl?: string | null;
  mfaRequired?: boolean;
  mfaChallengeToken?: string | null;
  mfaQuestionId?: string | null;
  mfaQuestionText?: string | null;
  mfaExpiresAt?: string | null;
}

export interface SessionUser {
  userId: string;
  email: string;
  name: string;
  role: string;
  profilePictureUrl?: string | null;
}
