/* Generated from Identity.Api OpenAPI. Do not edit; run npm run api:generate. */
export interface paths {
  '/api/v1/Auth/invitations/accept': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get?: never;
    put?: never;
    post: {
      parameters: {
        query?: never;
        header?: never;
        path?: never;
        cookie?: never;
      };
      requestBody: {
        content: {
          'application/json': components['schemas']['AcceptInvitationRequest'];
          'text/json': components['schemas']['AcceptInvitationRequest'];
          'application/*+json': components['schemas']['AcceptInvitationRequest'];
        };
      };
      responses: {
        /** @description No Content */
        204: {
          headers: {
            [name: string]: unknown;
          };
          content?: never;
        };
      };
    };
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  '/api/v1/Auth/password/forgot': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get?: never;
    put?: never;
    post: {
      parameters: {
        query?: never;
        header?: never;
        path?: never;
        cookie?: never;
      };
      requestBody: {
        content: {
          'application/json': components['schemas']['ForgotPasswordRequest'];
          'text/json': components['schemas']['ForgotPasswordRequest'];
          'application/*+json': components['schemas']['ForgotPasswordRequest'];
        };
      };
      responses: {
        /** @description Accepted */
        202: {
          headers: {
            [name: string]: unknown;
          };
          content: {
            'text/plain': components['schemas']['VerificationAcceptedResponse'];
            'application/json': components['schemas']['VerificationAcceptedResponse'];
            'text/json': components['schemas']['VerificationAcceptedResponse'];
          };
        };
      };
    };
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  '/api/v1/Auth/password/reset': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get?: never;
    put?: never;
    post: {
      parameters: {
        query?: never;
        header?: never;
        path?: never;
        cookie?: never;
      };
      requestBody: {
        content: {
          'application/json': components['schemas']['ResetPasswordRequest'];
          'text/json': components['schemas']['ResetPasswordRequest'];
          'application/*+json': components['schemas']['ResetPasswordRequest'];
        };
      };
      responses: {
        /** @description No Content */
        204: {
          headers: {
            [name: string]: unknown;
          };
          content?: never;
        };
      };
    };
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  '/api/v1/Auth/password/change': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get?: never;
    put?: never;
    post: {
      parameters: {
        query?: never;
        header?: never;
        path?: never;
        cookie?: never;
      };
      requestBody: {
        content: {
          'application/json': components['schemas']['ChangePasswordRequest'];
          'text/json': components['schemas']['ChangePasswordRequest'];
          'application/*+json': components['schemas']['ChangePasswordRequest'];
        };
      };
      responses: {
        /** @description No Content */
        204: {
          headers: {
            [name: string]: unknown;
          };
          content?: never;
        };
      };
    };
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  '/api/v1/Auth/refresh': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get?: never;
    put?: never;
    post: {
      parameters: {
        query?: never;
        header?: never;
        path?: never;
        cookie?: never;
      };
      requestBody: {
        content: {
          'application/json': components['schemas']['RefreshTokenRequest'];
          'text/json': components['schemas']['RefreshTokenRequest'];
          'application/*+json': components['schemas']['RefreshTokenRequest'];
        };
      };
      responses: {
        /** @description OK */
        200: {
          headers: {
            [name: string]: unknown;
          };
          content: {
            'text/plain': components['schemas']['TokenPairResult'];
            'application/json': components['schemas']['TokenPairResult'];
            'text/json': components['schemas']['TokenPairResult'];
          };
        };
        /** @description Unauthorized */
        401: {
          headers: {
            [name: string]: unknown;
          };
          content: {
            'text/plain': components['schemas']['ProblemDetails'];
            'application/json': components['schemas']['ProblemDetails'];
            'text/json': components['schemas']['ProblemDetails'];
          };
        };
      };
    };
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  '/api/v1/Auth/logout': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get?: never;
    put?: never;
    post: {
      parameters: {
        query?: never;
        header?: never;
        path?: never;
        cookie?: never;
      };
      requestBody: {
        content: {
          'application/json': components['schemas']['RefreshTokenRequest'];
          'text/json': components['schemas']['RefreshTokenRequest'];
          'application/*+json': components['schemas']['RefreshTokenRequest'];
        };
      };
      responses: {
        /** @description No Content */
        204: {
          headers: {
            [name: string]: unknown;
          };
          content?: never;
        };
      };
    };
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  '/api/v1/Auth/logout-all': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get?: never;
    put?: never;
    post: {
      parameters: {
        query?: never;
        header?: never;
        path?: never;
        cookie?: never;
      };
      requestBody?: never;
      responses: {
        /** @description No Content */
        204: {
          headers: {
            [name: string]: unknown;
          };
          content?: never;
        };
      };
    };
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  '/api/v1/Auth/login': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get?: never;
    put?: never;
    post: {
      parameters: {
        query?: never;
        header?: never;
        path?: never;
        cookie?: never;
      };
      requestBody: {
        content: {
          'application/json': components['schemas']['LoginRequest'];
          'text/json': components['schemas']['LoginRequest'];
          'application/*+json': components['schemas']['LoginRequest'];
        };
      };
      responses: {
        /** @description OK */
        200: {
          headers: {
            [name: string]: unknown;
          };
          content: {
            'text/plain': components['schemas']['TokenPairResult'];
            'application/json': components['schemas']['TokenPairResult'];
            'text/json': components['schemas']['TokenPairResult'];
          };
        };
        /** @description Unauthorized */
        401: {
          headers: {
            [name: string]: unknown;
          };
          content: {
            'text/plain': components['schemas']['ProblemDetails'];
            'application/json': components['schemas']['ProblemDetails'];
            'text/json': components['schemas']['ProblemDetails'];
          };
        };
      };
    };
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  '/api/v1/Auth/register': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get?: never;
    put?: never;
    post: {
      parameters: {
        query?: never;
        header?: never;
        path?: never;
        cookie?: never;
      };
      requestBody: {
        content: {
          'application/json': components['schemas']['RegisterRequest'];
          'text/json': components['schemas']['RegisterRequest'];
          'application/*+json': components['schemas']['RegisterRequest'];
        };
      };
      responses: {
        /** @description Accepted */
        202: {
          headers: {
            [name: string]: unknown;
          };
          content: {
            'text/plain': components['schemas']['VerificationAcceptedResponse'];
            'application/json': components['schemas']['VerificationAcceptedResponse'];
            'text/json': components['schemas']['VerificationAcceptedResponse'];
          };
        };
      };
    };
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  '/api/v1/Auth/email/verification-request': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get?: never;
    put?: never;
    post: {
      parameters: {
        query?: never;
        header?: never;
        path?: never;
        cookie?: never;
      };
      requestBody: {
        content: {
          'application/json': components['schemas']['EmailVerificationRequest'];
          'text/json': components['schemas']['EmailVerificationRequest'];
          'application/*+json': components['schemas']['EmailVerificationRequest'];
        };
      };
      responses: {
        /** @description Accepted */
        202: {
          headers: {
            [name: string]: unknown;
          };
          content: {
            'text/plain': components['schemas']['VerificationAcceptedResponse'];
            'application/json': components['schemas']['VerificationAcceptedResponse'];
            'text/json': components['schemas']['VerificationAcceptedResponse'];
          };
        };
      };
    };
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  '/api/v1/Auth/email/verify': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get?: never;
    put?: never;
    post: {
      parameters: {
        query?: never;
        header?: never;
        path?: never;
        cookie?: never;
      };
      requestBody: {
        content: {
          'application/json': components['schemas']['VerifyEmailRequest'];
          'text/json': components['schemas']['VerifyEmailRequest'];
          'application/*+json': components['schemas']['VerifyEmailRequest'];
        };
      };
      responses: {
        /** @description No Content */
        204: {
          headers: {
            [name: string]: unknown;
          };
          content?: never;
        };
      };
    };
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  '/.well-known/jwks.json': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get: {
      parameters: {
        query?: never;
        header?: never;
        path?: never;
        cookie?: never;
      };
      requestBody?: never;
      responses: {
        /** @description OK */
        200: {
          headers: {
            [name: string]: unknown;
          };
          content: {
            'text/plain': components['schemas']['JwksResponse'];
            'application/json': components['schemas']['JwksResponse'];
            'text/json': components['schemas']['JwksResponse'];
          };
        };
      };
    };
    put?: never;
    post?: never;
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  '/health/live': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get: {
      parameters: {
        query?: never;
        header?: never;
        path?: never;
        cookie?: never;
      };
      requestBody?: never;
      responses: {
        /** @description OK */
        200: {
          headers: {
            [name: string]: unknown;
          };
          content: {
            'application/json': components['schemas']['HealthResponse'];
          };
        };
      };
    };
    put?: never;
    post?: never;
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  '/health/ready': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get: {
      parameters: {
        query?: never;
        header?: never;
        path?: never;
        cookie?: never;
      };
      requestBody?: never;
      responses: {
        /** @description OK */
        200: {
          headers: {
            [name: string]: unknown;
          };
          content: {
            'application/json': components['schemas']['HealthResponse'];
          };
        };
        /** @description Service Unavailable */
        503: {
          headers: {
            [name: string]: unknown;
          };
          content: {
            'application/json': components['schemas']['HealthResponse'];
          };
        };
      };
    };
    put?: never;
    post?: never;
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  '/api/v1/Sessions': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get: {
      parameters: {
        query?: never;
        header?: never;
        path?: never;
        cookie?: never;
      };
      requestBody?: never;
      responses: {
        /** @description OK */
        200: {
          headers: {
            [name: string]: unknown;
          };
          content: {
            'text/plain': components['schemas']['SessionSummary'][];
            'application/json': components['schemas']['SessionSummary'][];
            'text/json': components['schemas']['SessionSummary'][];
          };
        };
      };
    };
    put?: never;
    post?: never;
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  '/api/v1/Sessions/{id}': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get?: never;
    put?: never;
    post?: never;
    delete: {
      parameters: {
        query?: never;
        header?: never;
        path: {
          id: string;
        };
        cookie?: never;
      };
      requestBody?: never;
      responses: {
        /** @description No Content */
        204: {
          headers: {
            [name: string]: unknown;
          };
          content?: never;
        };
        /** @description Not Found */
        404: {
          headers: {
            [name: string]: unknown;
          };
          content: {
            'text/plain': components['schemas']['ProblemDetails'];
            'application/json': components['schemas']['ProblemDetails'];
            'text/json': components['schemas']['ProblemDetails'];
          };
        };
      };
    };
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  '/api/v1/Users/me': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get: {
      parameters: {
        query?: never;
        header?: never;
        path?: never;
        cookie?: never;
      };
      requestBody?: never;
      responses: {
        /** @description OK */
        200: {
          headers: {
            [name: string]: unknown;
          };
          content: {
            'text/plain': components['schemas']['UserProfile'];
            'application/json': components['schemas']['UserProfile'];
            'text/json': components['schemas']['UserProfile'];
          };
        };
      };
    };
    put?: never;
    post?: never;
    delete?: never;
    options?: never;
    head?: never;
    patch: {
      parameters: {
        query?: never;
        header?: never;
        path?: never;
        cookie?: never;
      };
      requestBody: {
        content: {
          'application/json': components['schemas']['UpdateProfileRequest'];
          'text/json': components['schemas']['UpdateProfileRequest'];
          'application/*+json': components['schemas']['UpdateProfileRequest'];
        };
      };
      responses: {
        /** @description OK */
        200: {
          headers: {
            [name: string]: unknown;
          };
          content: {
            'text/plain': components['schemas']['UserProfile'];
            'application/json': components['schemas']['UserProfile'];
            'text/json': components['schemas']['UserProfile'];
          };
        };
      };
    };
    trace?: never;
  };
  '/api/v1/Users/me/deactivate': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get?: never;
    put?: never;
    post: {
      parameters: {
        query?: never;
        header?: never;
        path?: never;
        cookie?: never;
      };
      requestBody: {
        content: {
          'application/json': components['schemas']['DeactivateAccountRequest'];
          'text/json': components['schemas']['DeactivateAccountRequest'];
          'application/*+json': components['schemas']['DeactivateAccountRequest'];
        };
      };
      responses: {
        /** @description No Content */
        204: {
          headers: {
            [name: string]: unknown;
          };
          content?: never;
        };
      };
    };
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  '/api/v1/admin/audit-events': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get: {
      parameters: {
        query?: {
          Page?: number | string;
          PageSize?: number | string;
          UserId?: string;
          ActorUserId?: string;
          EventType?: string;
          From?: string;
          To?: string;
        };
        header?: never;
        path?: never;
        cookie?: never;
      };
      requestBody?: never;
      responses: {
        /** @description OK */
        200: {
          headers: {
            [name: string]: unknown;
          };
          content: {
            'text/plain': components['schemas']['PageResultOfAdminAuditEvent'];
            'application/json': components['schemas']['PageResultOfAdminAuditEvent'];
            'text/json': components['schemas']['PageResultOfAdminAuditEvent'];
          };
        };
      };
    };
    put?: never;
    post?: never;
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  '/api/v1/admin/auth/csrf': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get: {
      parameters: {
        query?: never;
        header?: never;
        path?: never;
        cookie?: never;
      };
      requestBody?: never;
      responses: {
        /** @description OK */
        200: {
          headers: {
            [name: string]: unknown;
          };
          content: {
            'text/plain': components['schemas']['AdminCsrfResponse'];
            'application/json': components['schemas']['AdminCsrfResponse'];
            'text/json': components['schemas']['AdminCsrfResponse'];
          };
        };
      };
    };
    put?: never;
    post?: never;
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  '/api/v1/admin/auth/login': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get?: never;
    put?: never;
    post: {
      parameters: {
        query?: never;
        header?: never;
        path?: never;
        cookie?: never;
      };
      requestBody: {
        content: {
          'application/json': components['schemas']['AdminLoginRequest'];
          'text/json': components['schemas']['AdminLoginRequest'];
          'application/*+json': components['schemas']['AdminLoginRequest'];
        };
      };
      responses: {
        /** @description OK */
        200: {
          headers: {
            [name: string]: unknown;
          };
          content: {
            'text/plain': components['schemas']['AdminProfile'];
            'application/json': components['schemas']['AdminProfile'];
            'text/json': components['schemas']['AdminProfile'];
          };
        };
      };
    };
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  '/api/v1/admin/auth/me': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get: {
      parameters: {
        query?: never;
        header?: never;
        path?: never;
        cookie?: never;
      };
      requestBody?: never;
      responses: {
        /** @description OK */
        200: {
          headers: {
            [name: string]: unknown;
          };
          content: {
            'text/plain': components['schemas']['AdminProfile'];
            'application/json': components['schemas']['AdminProfile'];
            'text/json': components['schemas']['AdminProfile'];
          };
        };
      };
    };
    put?: never;
    post?: never;
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  '/api/v1/admin/auth/logout': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get?: never;
    put?: never;
    post: {
      parameters: {
        query?: never;
        header?: never;
        path?: never;
        cookie?: never;
      };
      requestBody?: never;
      responses: {
        /** @description No Content */
        204: {
          headers: {
            [name: string]: unknown;
          };
          content?: never;
        };
      };
    };
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  '/api/v1/admin/dashboard': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get: {
      parameters: {
        query?: never;
        header?: never;
        path?: never;
        cookie?: never;
      };
      requestBody?: never;
      responses: {
        /** @description OK */
        200: {
          headers: {
            [name: string]: unknown;
          };
          content: {
            'text/plain': components['schemas']['DashboardResult'];
            'application/json': components['schemas']['DashboardResult'];
            'text/json': components['schemas']['DashboardResult'];
          };
        };
      };
    };
    put?: never;
    post?: never;
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  '/api/v1/admin/users/invitations': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get?: never;
    put?: never;
    post: {
      parameters: {
        query?: never;
        header?: never;
        path?: never;
        cookie?: never;
      };
      requestBody: {
        content: {
          'application/json': components['schemas']['InviteUserRequest'];
          'text/json': components['schemas']['InviteUserRequest'];
          'application/*+json': components['schemas']['InviteUserRequest'];
        };
      };
      responses: {
        /** @description Accepted */
        202: {
          headers: {
            [name: string]: unknown;
          };
          content?: never;
        };
      };
    };
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  '/api/v1/admin/users/{userId}/invitations/resend': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get?: never;
    put?: never;
    post: {
      parameters: {
        query?: never;
        header?: never;
        path: {
          userId: string;
        };
        cookie?: never;
      };
      requestBody?: never;
      responses: {
        /** @description Accepted */
        202: {
          headers: {
            [name: string]: unknown;
          };
          content?: never;
        };
      };
    };
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  '/api/v1/admin/users/{userId}/password-reset': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get?: never;
    put?: never;
    post: {
      parameters: {
        query?: never;
        header?: never;
        path: {
          userId: string;
        };
        cookie?: never;
      };
      requestBody?: never;
      responses: {
        /** @description Accepted */
        202: {
          headers: {
            [name: string]: unknown;
          };
          content?: never;
        };
      };
    };
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  '/api/v1/admin/users/{userId}/disable': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get?: never;
    put?: never;
    post: {
      parameters: {
        query?: never;
        header?: never;
        path: {
          userId: string;
        };
        cookie?: never;
      };
      requestBody: {
        content: {
          'application/json': components['schemas']['DisableUserRequest'];
          'text/json': components['schemas']['DisableUserRequest'];
          'application/*+json': components['schemas']['DisableUserRequest'];
        };
      };
      responses: {
        /** @description No Content */
        204: {
          headers: {
            [name: string]: unknown;
          };
          content?: never;
        };
      };
    };
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  '/api/v1/admin/users/{userId}/enable': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get?: never;
    put?: never;
    post: {
      parameters: {
        query?: never;
        header?: never;
        path: {
          userId: string;
        };
        cookie?: never;
      };
      requestBody?: never;
      responses: {
        /** @description No Content */
        204: {
          headers: {
            [name: string]: unknown;
          };
          content?: never;
        };
      };
    };
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  '/api/v1/admin/users/{userId}/sessions/{sessionId}': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get?: never;
    put?: never;
    post?: never;
    delete: {
      parameters: {
        query?: never;
        header?: never;
        path: {
          userId: string;
          sessionId: string;
        };
        cookie?: never;
      };
      requestBody?: never;
      responses: {
        /** @description No Content */
        204: {
          headers: {
            [name: string]: unknown;
          };
          content?: never;
        };
      };
    };
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  '/api/v1/admin/users/{userId}/sessions/revoke-all': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get?: never;
    put?: never;
    post: {
      parameters: {
        query?: never;
        header?: never;
        path: {
          userId: string;
        };
        cookie?: never;
      };
      requestBody?: never;
      responses: {
        /** @description No Content */
        204: {
          headers: {
            [name: string]: unknown;
          };
          content?: never;
        };
      };
    };
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  '/api/v1/admin/users': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get: {
      parameters: {
        query?: {
          Page?: number | string;
          PageSize?: number | string;
          Search?: string;
          Status?: string;
          EmailVerified?: boolean;
          SortBy?: string;
          SortDirection?: string;
        };
        header?: never;
        path?: never;
        cookie?: never;
      };
      requestBody?: never;
      responses: {
        /** @description OK */
        200: {
          headers: {
            [name: string]: unknown;
          };
          content: {
            'text/plain': components['schemas']['PageResultOfAdminUserListItem'];
            'application/json': components['schemas']['PageResultOfAdminUserListItem'];
            'text/json': components['schemas']['PageResultOfAdminUserListItem'];
          };
        };
      };
    };
    put?: never;
    post?: never;
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  '/api/v1/admin/users/{userId}': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get: {
      parameters: {
        query?: never;
        header?: never;
        path: {
          userId: string;
        };
        cookie?: never;
      };
      requestBody?: never;
      responses: {
        /** @description OK */
        200: {
          headers: {
            [name: string]: unknown;
          };
          content: {
            'text/plain': components['schemas']['AdminUserDetails'];
            'application/json': components['schemas']['AdminUserDetails'];
            'text/json': components['schemas']['AdminUserDetails'];
          };
        };
        /** @description Not Found */
        404: {
          headers: {
            [name: string]: unknown;
          };
          content: {
            'text/plain': components['schemas']['ProblemDetails'];
            'application/json': components['schemas']['ProblemDetails'];
            'text/json': components['schemas']['ProblemDetails'];
          };
        };
      };
    };
    put?: never;
    post?: never;
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
  '/api/v1/admin/users/{userId}/sessions': {
    parameters: {
      query?: never;
      header?: never;
      path?: never;
      cookie?: never;
    };
    get: {
      parameters: {
        query?: {
          page?: number | string;
          pageSize?: number | string;
          state?: string;
        };
        header?: never;
        path: {
          userId: string;
        };
        cookie?: never;
      };
      requestBody?: never;
      responses: {
        /** @description OK */
        200: {
          headers: {
            [name: string]: unknown;
          };
          content: {
            'text/plain': components['schemas']['PageResultOfAdminUserSession'];
            'application/json': components['schemas']['PageResultOfAdminUserSession'];
            'text/json': components['schemas']['PageResultOfAdminUserSession'];
          };
        };
        /** @description Not Found */
        404: {
          headers: {
            [name: string]: unknown;
          };
          content: {
            'text/plain': components['schemas']['ProblemDetails'];
            'application/json': components['schemas']['ProblemDetails'];
            'text/json': components['schemas']['ProblemDetails'];
          };
        };
      };
    };
    put?: never;
    post?: never;
    delete?: never;
    options?: never;
    head?: never;
    patch?: never;
    trace?: never;
  };
}
export type webhooks = Record<string, never>;
export interface components {
  schemas: {
    AcceptInvitationRequest: {
      token?: null | string;
      newPassword?: null | string;
    };
    AdminAuditEvent: {
      /** Format: uuid */
      id: string;
      /** Format: uuid */
      userId: null | string;
      /** Format: uuid */
      actorUserId: null | string;
      /** Format: uuid */
      targetUserId: null | string;
      eventType: string;
      /** Format: date-time */
      occurredAt: string;
      clientId: null | string;
      correlationId: string;
    };
    AdminCsrfResponse: {
      token: string;
    };
    AdminLoginRequest: {
      email?: null | string;
      password?: null | string;
    };
    AdminProfile: {
      /** Format: uuid */
      id: string;
      email: string;
      displayName: null | string;
      role: string;
    };
    AdminUserDetails: {
      /** Format: uuid */
      id: string;
      email: string;
      displayName: null | string;
      status: string;
      emailVerified: boolean;
      /** Format: date-time */
      emailVerifiedAt: null | string;
      /** Format: date-time */
      createdAt: string;
      /** Format: date-time */
      updatedAt: string;
      /** Format: date-time */
      deactivatedAt: null | string;
      /** Format: date-time */
      disabledAt: null | string;
      /** Format: uuid */
      disabledBy: null | string;
      disableReason: null | string;
      /** Format: int32 */
      failedLoginCount: number | string;
      /** Format: date-time */
      lockedUntil: null | string;
    };
    AdminUserListItem: {
      /** Format: uuid */
      id: string;
      email: string;
      displayName: null | string;
      status: string;
      emailVerified: boolean;
      /** Format: date-time */
      createdAt: string;
    };
    AdminUserSession: {
      /** Format: uuid */
      id: string;
      clientId: string;
      userAgent: null | string;
      /** Format: date-time */
      createdAt: string;
      /** Format: date-time */
      lastUsedAt: string;
      /** Format: date-time */
      absoluteExpiresAt: string;
      /** Format: date-time */
      revokedAt: null | string;
      revokeReason: null | string;
      isActive: boolean;
    };
    ChangePasswordRequest: {
      currentPassword?: null | string;
      newPassword?: null | string;
    };
    DashboardResult: {
      /** Format: int64 */
      totalUsers: number | string;
      /** Format: int64 */
      activeUsers: number | string;
      /** Format: int64 */
      pendingVerificationUsers: number | string;
      /** Format: int64 */
      invitedUsers: number | string;
      /** Format: int64 */
      disabledUsers: number | string;
      /** Format: int64 */
      deactivatedUsers: number | string;
      /** Format: int64 */
      activeSessions: number | string;
      registrationTrend: components['schemas']['RegistrationTrendPoint'][];
      /** Format: date-time */
      asOf: string;
      /** Format: date-time */
      registrationTrendFrom: string;
      /** Format: date-time */
      registrationTrendTo: string;
    };
    DeactivateAccountRequest: {
      password?: null | string;
    };
    DisableUserRequest: {
      reason?: null | string;
    };
    EmailVerificationRequest: {
      email?: null | string;
    };
    ForgotPasswordRequest: {
      email?: null | string;
    };
    HealthResponse: {
      status: string;
    };
    InviteUserRequest: {
      email?: null | string;
      displayName?: null | string;
    };
    JwksResponse: {
      keys: components['schemas']['PublicJwk'][];
    };
    LoginRequest: {
      email?: null | string;
      password?: null | string;
      clientId?: null | string;
    };
    PageResultOfAdminAuditEvent: {
      items: components['schemas']['AdminAuditEvent'][];
      /** Format: int32 */
      page: number | string;
      /** Format: int32 */
      pageSize: number | string;
      /** Format: int64 */
      total: number | string;
    };
    PageResultOfAdminUserListItem: {
      items: components['schemas']['AdminUserListItem'][];
      /** Format: int32 */
      page: number | string;
      /** Format: int32 */
      pageSize: number | string;
      /** Format: int64 */
      total: number | string;
    };
    PageResultOfAdminUserSession: {
      items: components['schemas']['AdminUserSession'][];
      /** Format: int32 */
      page: number | string;
      /** Format: int32 */
      pageSize: number | string;
      /** Format: int64 */
      total: number | string;
    };
    ProblemDetails: {
      type?: null | string;
      title?: null | string;
      /** Format: int32 */
      status?: null | number | string;
      detail?: null | string;
      instance?: null | string;
    };
    PublicJwk: {
      kty: string;
      use: string;
      alg: string;
      kid: string;
      n: string;
      e: string;
    };
    RefreshTokenRequest: {
      refreshToken?: null | string;
    };
    RegisterRequest: {
      email?: null | string;
      password?: null | string;
      displayName?: null | string;
      clientId?: null | string;
    };
    RegistrationTrendPoint: {
      /** Format: date */
      date: string;
      /** Format: int64 */
      count: number | string;
    };
    ResetPasswordRequest: {
      token?: null | string;
      newPassword?: null | string;
    };
    SessionSummary: {
      /** Format: uuid */
      id: string;
      clientId: string;
      /** Format: date-time */
      createdAt: string;
      /** Format: date-time */
      lastUsedAt: string;
      /** Format: date-time */
      absoluteExpiresAt: string;
      userAgent: null | string;
      isCurrent: boolean;
    };
    TokenPairResult: {
      accessToken: string;
      refreshToken: string;
      tokenType?: null | string;
      /** Format: int32 */
      expiresIn: number | string;
    };
    UpdateProfileRequest: {
      displayName: null | string;
    };
    UserProfile: {
      /** Format: uuid */
      id: string;
      email: string;
      displayName: null | string;
      emailVerified: boolean;
      status: string;
      /** Format: date-time */
      createdAt: string;
    };
    VerificationAcceptedResponse: {
      message: string;
    };
    VerifyEmailRequest: {
      token?: null | string;
    };
  };
  responses: never;
  parameters: never;
  requestBodies: never;
  headers: never;
  pathItems: never;
}
export type $defs = Record<string, never>;
export type operations = Record<string, never>;
