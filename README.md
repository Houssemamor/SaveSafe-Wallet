# SaveSafe Wallet (SSW)

Secure web-based banking & wallet platform with AI anti-pirating system.

**Stack**: .NET 8 (C# microservices) · Firestore (Firebase) · Redis · Kafka · Grafana · Loki · Angular (Sprint 2+)

**Team**: AMOR Houssem · KHEDHIRI Montaha · BEN SLIMA Wissem · BAHROUNI Seif
**Supervisor**: Mme. Ines AGREBI

---

## Sprint 1 — What's Running

| Service | Port | Description |
|---|---|---|
| Frontend (Angular) | 4200 | login/register/dashboard/profile/wallet history |
| Auth Service | 5001 | register, login, refresh, logout · JWT · RBAC |
| Wallet Service | 5002 | wallet balance · ledger · internal provisioning |
| Firestore (Firebase) | cloud | primary database |
| Redis | 6379 | sessions, rate-limiting (Sprint 4) |
| Kafka + Zookeeper | 9092 | async messaging (Sprint 3) |
| Grafana | 3000 | dashboards (admin: `admin`/`admin`) |
| Loki | 3100 | centralized log aggregation |

---

## Prerequisites

### Required Software

- **Docker Desktop** ≥ 4.x (Docker Compose v2 included)
  - Required for containerized development and production-like environment
  - Provides consistent runtime environment across all platforms
  - Eliminates local dependency conflicts and simplifies onboarding
- **Git**
  - Required for version control and repository management

### Firebase Configuration

- **Firebase project** with Firestore enabled
- **Service account JSON** with appropriate permissions
  - Must include: Firestore Datastore User, Service Account Token Creator
  - Required for backend services to access Firestore
  - Store securely: never commit to version control, use environment-specific credentials

### Security Requirements

**Firebase Credentials Security:**
- Service account JSON files contain sensitive credentials that grant administrative access
- Never commit service account JSON files to version control
- Use separate Firebase projects for development, staging, and production
- Rotate service account keys immediately if compromised or accidentally exposed
- Apply principle of least privilege: grant only necessary permissions to service accounts

**Environment Variables Security:**
- `.env` file contains sensitive configuration (JWT secrets, API keys, credentials)
- Never commit `.env` files to version control (already in `.gitignore`)
- Use strong, unique values for `JWT_SECRET_KEY` and `INTERNAL_API_KEY`
- Rotate secrets regularly and after any security incident
- Use different secrets for different environments

### Optional Development Tools

> .NET 8 SDK is **not** needed locally — everything runs in containers.
> Install .NET 8 SDK only if you want to run services outside Docker for debugging or development purposes.

**When to install .NET 8 SDK:**
- Running services directly without Docker for faster iteration
- Debugging with Visual Studio or VS Code
- Running unit tests locally
- Performance profiling or advanced debugging

**Installation:** [Download .NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

---

## Quick Start

### Initial Setup

```bash
# 1. Clone the repo
git clone <repo-url>
cd projectDev

# 2. Create your local .env (never commit this file)
cp .env.example .env
# Edit .env and set JWT_SECRET_KEY, INTERNAL_API_KEY, FIRESTORE_PROJECT_ID,
# and FIRESTORE_CREDENTIALS_PATH (path to your service account JSON)
# Place the service account JSON at ./secrets/firebase.json unless you override the path

# 3. Start the full stack
docker compose up -d

# 4. Watch logs (optional)
docker compose logs -f frontend auth-service wallet-service
```

### Validation and Verification

After startup, wait ~30 seconds for Kafka to become ready, then verify each service:

**1. Check container status:**
```bash
docker compose ps
```
Expected output: All services should show "Up" status with healthy healthchecks where configured.

**2. Verify service health endpoints:**

**Auth Service Health Check:**
```bash
curl http://localhost:5001/health
```
Purpose: Verifies Auth Service is running and can connect to Firestore
Expected: `200 OK` with service status information

**Wallet Service Health Check:**
```bash
curl http://localhost:5002/health
```
Purpose: Verifies Wallet Service is running and can connect to Firestore
Expected: `200 OK` with service status information

**Frontend Application:**
```bash
curl http://localhost:4200
```
Purpose: Verifies Angular frontend is serving correctly
Expected: `200 OK` with HTML response

**3. Verify application functionality:**

- **Frontend**: http://localhost:4200
  - Purpose: Access main application interface
  - Expected: Login page loads successfully

- **Admin Dashboard**: http://localhost:4200/admin
  - Purpose: Access admin interface (requires Admin role)
  - Expected: Redirects to login if not authenticated, shows admin dashboard if authenticated as admin

- **Swagger Documentation** (dev mode only): http://localhost:5001/swagger
  - Purpose: Interactive API documentation for Auth Service
  - Expected: Swagger UI loads with all endpoints documented

- **Grafana Monitoring**: http://localhost:3000 (login: admin / admin)
  - Purpose: View system metrics and logs
  - Expected: Grafana dashboard loads with Loki datasource configured

### Default Admin Account

Default admin bootstrap account (created automatically if no admin exists):
- Email: `admin@savesafe.local`
- Password: `Admin@12345!`

You can override these values with `DEFAULT_ADMIN_EMAIL`, `DEFAULT_ADMIN_NAME`, and `DEFAULT_ADMIN_PASSWORD` in `.env`.

### Troubleshooting Common Issues

**Issue: Services fail to start**
- Check Docker Desktop is running: `docker ps`
- Verify ports 4200, 5001, 5002, 3000 are not in use
- Check logs: `docker compose logs <service-name>`
- Ensure `.env` file exists with required values

**Issue: Auth/Wallet services crash immediately**
- Verify Firebase credentials are correctly configured
- Check service account JSON file exists at specified path
- Ensure Firebase project has Firestore enabled
- Check service account has necessary permissions

**Issue: Frontend cannot connect to backend**
- Verify backend services are healthy: `curl http://localhost:5001/health`
- Check network connectivity between containers
- Verify API URLs in frontend configuration match backend ports

**Issue: "Connection refused" errors**
- Wait 30-60 seconds for Kafka to fully start (Kafka takes time to initialize)
- Check Kafka health: `docker compose logs kafka`
- Restart services: `docker compose restart <service-name>`

**Issue: Health checks failing**
- Check Firestore connectivity: Verify credentials and project ID
- Ensure Firebase project is not in a suspended state
- Check network connectivity to Firebase endpoints

**Issue: Admin account not created**
- Check AuthService logs for seeder errors
- Verify `DEFAULT_ADMIN_*` environment variables are set
- Ensure no admin account already exists in Firestore

**Issue: Rate limiting errors during testing**
- Rate limits are enforced via Redis (10 req/min for auth endpoints)
- Use different test accounts or wait for rate limit window to reset
- Check Redis is running: `docker compose ps redis`

---

## Firestore Credentials and API Key

### Service account (backend)

1. Create a Firebase project and enable Firestore.
2. Create a service account and download the JSON key.
3. Place the JSON at `./secrets/firebase.json` (this folder is gitignored).
4. Set `.env` values:
  - `FIRESTORE_PROJECT_ID`
  - `FIRESTORE_CREDENTIALS_PATH` (defaults to `./secrets/firebase.json`)
5. If running outside Docker, export either:
  - `Firestore__ProjectId` and `Firestore__CredentialsPath`, or
  - `GOOGLE_APPLICATION_CREDENTIALS` (path to the JSON file)

### Firebase Web API key (frontend)

1. In Firebase console, open Project settings → General.
2. Copy the Web API Key and set `FIREBASE_WEB_API_KEY` in `.env`.
3. If you add Firebase Web SDK to Angular, wire the key into the environment config.

### Google OAuth Authentication

The application supports Google OAuth authentication. To enable it:

1. **Setup Firebase Configuration**:
   ```bash
   # Run setup script
   ./setup-firebase.sh        # Linux/Mac
   setup-firebase.bat          # Windows
   ```

2. **Configure Firebase Console**:
   - Go to Firebase Console → Authentication → Sign-in method
   - Enable **Google** sign-in
   - Add authorized domains: `localhost`, `127.0.0.1`

3. **Update Environment Files**:
   - Copy Firebase config to `src/frontend/.env`
   - Update `src/frontend/src/environments/environment.ts`

4. **Test Google Login**:
   - Navigate to `http://localhost:4200/login`
   - Click "Continue with Google"
   - Complete OAuth flow

---

## API Documentation

### Authentication Flow

The application uses JWT-based authentication with refresh tokens:

1. **Registration/Login**: User provides credentials → Server validates → Returns JWT access token (15min expiry) + sets httpOnly refresh cookie (7 days)
2. **Access Protected Resources**: Include JWT in `Authorization: Bearer <token>` header
3. **Token Refresh**: When access token expires, call `/api/auth/refresh` with refresh cookie → New access token issued
4. **Logout**: Access token invalidated + refresh cookie cleared

**Security Model**:
- Access tokens are short-lived (15 minutes) to limit exposure window
- Refresh tokens stored in httpOnly cookies (not accessible via JavaScript)
- All protected endpoints validate JWT signature and expiration
- Refresh tokens are single-use and rotated on each refresh

### Rate Limiting

Current rate limits (enforced via Redis, Sprint 4):
- **Auth endpoints**: 10 requests per minute per IP
- **Wallet operations**: 30 requests per minute per user
- **Admin endpoints**: 100 requests per minute per admin

Rate limit headers are included in responses:
- `X-RateLimit-Limit`: Request limit per window
- `X-RateLimit-Remaining`: Remaining requests in current window
- `X-RateLimit-Reset`: Unix timestamp when window resets

### API Examples

#### Register a new user

```bash
curl -X POST http://localhost:5001/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"alice@example.com","name":"Alice","password":"Secure@1234"}' \
  -c cookies.txt
```

**Success Response (201 Created):**
```json
{
  "accessToken": "eyJhbGci...",
  "tokenType": "Bearer",
  "expiresIn": 900,
  "userId": "3fa85f64-...",
  "email": "alice@example.com",
  "name": "Alice",
  "role": "User"
}
```
The `ssw_refresh` httpOnly cookie is set automatically.

**Error Responses:**

400 Bad Request (invalid input):
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Password": ["Password must be at least 8 characters and contain uppercase, lowercase, number, and special character."]
  }
}
```

409 Conflict (email already exists):
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.8",
  "title": "Conflict",
  "status": 409,
  "detail": "A user with this email already exists."
}
```

429 Too Many Requests (rate limited):
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.29",
  "title": "Too Many Requests",
  "status": 429,
  "detail": "Rate limit exceeded. Please try again later."
}
```

---

#### Login

```bash
curl -X POST http://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"alice@example.com","password":"Secure@1234"}' \
  -c cookies.txt
```

**Success Response (200 OK):**
```json
{
  "accessToken": "eyJhbGci...",
  "tokenType": "Bearer",
  "expiresIn": 900,
  "userId": "3fa85f64-...",
  "email": "alice@example.com",
  "name": "Alice",
  "role": "User"
}
```

**Error Responses:**

401 Unauthorized (invalid credentials):
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Invalid email or password."
}
```

403 Forbidden (account disabled):
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.3",
  "title": "Forbidden",
  "status": 403,
  "detail": "Account has been disabled. Please contact support."
}
```

---

#### Get wallet balance

```bash
export TOKEN="<accessToken from login>"

curl http://localhost:5002/api/wallet/balance \
  -H "Authorization: Bearer $TOKEN"
```

**Success Response (200 OK):**
```json
{
  "accountId": "...",
  "accountNumber": "SSW-0000000001",
  "currency": "USD",
  "balance": 0.00,
  "updatedAt": "2026-03-01T..."
}
```

**Error Responses:**

401 Unauthorized (missing or invalid token):
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Access token is missing or invalid."
}
```

404 Not Found (wallet not found):
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "detail": "Wallet not found for the authenticated user."
}
```

---

#### Refresh access token

```bash
curl -X POST http://localhost:5001/api/auth/refresh \
  -b cookies.txt -c cookies.txt
```

**Success Response (200 OK):**
```json
{
  "accessToken": "eyJhbGci...",
  "tokenType": "Bearer",
  "expiresIn": 900,
  "userId": "3fa85f64-...",
  "email": "alice@example.com",
  "name": "Alice",
  "role": "User"
}
```

**Error Responses:**

401 Unauthorized (invalid or expired refresh token):
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Invalid or expired refresh token. Please login again."
}
```

---

#### Logout

```bash
curl -X POST http://localhost:5001/api/auth/logout \
  -H "Authorization: Bearer $TOKEN" \
  -b cookies.txt
```

**Success Response (204 No Content)** — refresh cookie is cleared.

**Error Responses:**

401 Unauthorized (missing or invalid token):
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Access token is missing or invalid."
}
```

---

## Branch Strategy

| Branch | Purpose | Protection |
|---|---|---|
| `main` | Production-ready | 2 approvals + CI green |
| `develop` | Integration | 1 approval + CI green |
| `feature/SSW-XX-description` | Feature work | Branch off `develop` |
| `fix/SSW-XX-description` | Bug fixes | Branch off `develop` |
| `hotfix/SSW-XX-description` | Critical patches | Branch off `main` |

---

## Development Setup (running outside Docker)

### Local Development Constraints

When running services outside Docker, be aware of these constraints:
- You must manually install and configure all dependencies (.NET 8 SDK, Node.js, etc.)
- Local environment may differ from production, leading to "works on my machine" issues
- Some features (like Kafka integration) may require additional local setup
- Port conflicts may occur if services are already running in Docker

### Debugging Configuration

**For .NET Services (Auth/Wallet):**
- Use Visual Studio or VS Code with C# extension
- Set breakpoints in controllers, services, and repositories
- Use `dotnet run` with `--launch-profile https` for HTTPS debugging
- Enable detailed logging: set `Logging__LogLevel__Default=Debug` in environment variables

**For Angular Frontend:**
- Use VS Code with Angular extension or Chrome DevTools
- Run `ng serve` for hot-reload development
- Use Angular DevTools browser extension for component inspection
- Enable source maps in `angular.json` for better debugging experience

### Testing Guidance

**Unit Tests:**
```bash
# .NET services
cd src/AuthService/AuthService.Tests
dotnet test

# Angular frontend
cd src/frontend
npm test
```

**Integration Tests:**
- Use test Firebase project with isolated data
- Mock external dependencies (payment gateways, etc.)
- Run tests in CI/CD pipeline before merging

**Manual Testing Checklist:**
- [ ] User registration and login flow
- [ ] JWT token refresh mechanism
- [ ] Protected route access control
- [ ] Admin dashboard functionality
- [ ] Wallet balance retrieval
- [ ] Transaction history display
- [ ] Error handling and validation

### Running Services Locally

```bash
# Start only infrastructure (optional)
docker compose up redis -d

# Auth Service
cd src/AuthService/AuthService.API
dotnet run

# Wallet Service (separate terminal)
cd src/WalletService/WalletService.API
dotnet run
```

Before running locally, set `Firestore__ProjectId` and `Firestore__CredentialsPath`
or `GOOGLE_APPLICATION_CREDENTIALS` in your shell.

---

## Firestore Indexes

Create the following indexes in Firestore for production workloads:

- `users` collection: order by `createdAt` (descending)
- `loginEvents` collection: order by `timestamp` (descending)
- `failedLoginsByIp` collection: order by `failedAttempts` (descending), then `lastAttemptAt` (descending)
- `refreshTokens` collection: filter by `userId` and `isRevoked`
- `accounts/{accountId}/ledgerEntries` subcollection: order by `createdAt` (descending)

---

## Project Structure

```
projectDev/
├── .github/
│   ├── PULL_REQUEST_TEMPLATE.md
│   └── workflows/ci.yml              # GitHub Actions CI
├── docker/
│   ├── loki/loki-config.yaml
│   └── grafana/provisioning/           # Auto-provisions Loki datasource
├── src/
│   ├── AuthService/                    # .NET 8 WebAPI — auth, RBAC, JWT
│   └── WalletService/                  # .NET 8 WebAPI — wallet, ledger
├── docker-compose.yml
├── .env.example                        # Template — copy to .env
└── README.md
```

---

## Sprints Overview

| Sprint | Focus | Dates |
|---|---|---|
| **Sprint 1** ✅ | Foundation: Auth + Wallet + Docker | Feb 15 – Feb 28 |
| Sprint 2 | User Dashboard + Profile + Angular frontend | Mar 1 – Mar 14 |
| Sprint 3 | Stripe Payments + Kafka + Notifications | Mar 15 – Mar 28 |
| Sprint 4 | Security Service + AI Risk Scoring | Mar 29 – Apr 11 |
| Sprint 5 | Reporting + Tests + Hardening + CI/CD | Apr 12 – Apr 25 |