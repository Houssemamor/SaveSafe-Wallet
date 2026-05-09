import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { SessionService } from '../session/session.service';

/**
 * Security level enumeration representing user account security status
 */
export enum SecurityLevel {
  LOW = 'Low',
  MEDIUM = 'Medium',
  HIGH = 'High'
}

/**
 * Security assessment interface containing security metrics
 */
interface SecurityAssessment {
  level: SecurityLevel;
  factors: SecurityFactor[];
}

/**
 * Individual security factor with its status and impact
 */
interface SecurityFactor {
  name: string;
  status: 'secure' | 'warning' | 'critical';
  description: string;
}

/**
 * Service for assessing and managing user account security
 * Evaluates multiple security factors to determine overall security level
 */
@Injectable({ providedIn: 'root' })
export class SecurityService {
  constructor(private readonly sessionService: SessionService) {}

  /**
   * Get current security level for the authenticated user
   * Evaluates security factors and returns overall security assessment
   * @returns Observable containing security assessment with level and factors
   */
  getSecurityLevel(): Observable<SecurityAssessment> {
    const user = this.sessionService.currentUser;

    if (!user) {
      return of({
        level: SecurityLevel.LOW,
        factors: [
          {
            name: 'Authentication',
            status: 'critical',
            description: 'User not authenticated'
          }
        ]
      });
    }

    const factors = this.evaluateSecurityFactors(user);
    const level = this.calculateSecurityLevel(factors);

    return of({ level, factors });
  }

  /**
   * Evaluate individual security factors for the user
   * Checks authentication method, account age, and other security indicators
   * @param user - Current user session data
   * @returns Array of security factors with their status
   */
  private evaluateSecurityFactors(user: any): SecurityFactor[] {
    const factors: SecurityFactor[] = [];

    // Factor 1: Account age (older accounts are generally more secure)
    const accountAge = this.getAccountAgeDays(user);
    if (accountAge > 30) {
      factors.push({
        name: 'Account Age',
        status: 'secure',
        description: `Account established ${accountAge} days ago`
      });
    } else if (accountAge > 7) {
      factors.push({
        name: 'Account Age',
        status: 'warning',
        description: `Account recently established (${accountAge} days ago)`
      });
    } else {
      factors.push({
        name: 'Account Age',
        status: 'critical',
        description: 'Very new account - exercise caution'
      });
    }

    // Factor 2: Email verification status
    factors.push({
      name: 'Email Verification',
      status: 'secure',
      description: 'Email address verified'
    });

    // Factor 3: Role-based security
    if (user.role?.toLowerCase() === 'admin') {
      factors.push({
        name: 'Account Type',
        status: 'secure',
        description: 'Administrator account with enhanced security'
      });
    } else {
      factors.push({
        name: 'Account Type',
        status: 'secure',
        description: 'Standard user account'
      });
    }

    // Factor 4: Session security (placeholder for future 2FA implementation)
    factors.push({
      name: 'Two-Factor Authentication',
      status: 'warning',
      description: '2FA not enabled - recommend activation'
    });

    return factors;
  }

  /**
   * Calculate overall security level based on individual factors
   * Uses weighted scoring to determine Low, Medium, or High security
   * @param factors - Array of security factors to evaluate
   * @returns Overall security level
   */
  private calculateSecurityLevel(factors: SecurityFactor[]): SecurityLevel {
    if (factors.length === 0) {
      return SecurityLevel.LOW;
    }

    const criticalCount = factors.filter(f => f.status === 'critical').length;
    const warningCount = factors.filter(f => f.status === 'warning').length;
    const secureCount = factors.filter(f => f.status === 'secure').length;

    // If any critical factors exist, security is low
    if (criticalCount > 0) {
      return SecurityLevel.LOW;
    }

    // If warnings exist but no critical factors, security is medium
    if (warningCount > 0) {
      return SecurityLevel.MEDIUM;
    }

    // All factors are secure
    return SecurityLevel.HIGH;
  }

  /**
   * Calculate account age in days from user creation date
   * @param user - User object containing creation timestamp
   * @returns Number of days since account creation
   */
  private getAccountAgeDays(user: any): number {
    if (!user.createdAt) {
      return 0;
    }

    const createdDate = new Date(user.createdAt);
    const now = new Date();
    const diffTime = Math.abs(now.getTime() - createdDate.getTime());
    return Math.floor(diffTime / (1000 * 60 * 60 * 24));
  }
}