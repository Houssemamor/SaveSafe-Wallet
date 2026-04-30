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

- **Docker Desktop** ≥ 4.x (Docker Compose v2 included)
- **Git**
- **Firebase project** with Firestore enabled (service account JSON)

> .NET 8 SDK is **not** needed locally — everything runs in containers.
> Install .NET 8 SDK only if you want to run services outside Docker.

---

## Quick Start

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

# 5. Verify all services are healthy
docker compose ps
```

Wait ~30 seconds for Kafka to become ready. Then check:
- Frontend: http://localhost:4200
- Admin Dashboard: http://localhost:4200/admin
- Auth Service: http://localhost:5001/health
- Wallet Service: http://localhost:5002/health
- Swagger (Auth): http://localhost:5001/swagger (dev mode only)
- Grafana: http://localhost:3000 (login: admin / admin)

Default admin bootstrap account (created automatically if no admin exists):
- Email: `admin@savesafe.local`
- Password: `Admin@12345!`

You can override these values with `DEFAULT_ADMIN_EMAIL`, `DEFAULT_ADMIN_NAME`, and `DEFAULT_ADMIN_PASSWORD` in `.env`.

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

---

## API Examples

### Register a new user

```bash
curl -X POST http://localhost:5001/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"alice@example.com","name":"Alice","password":"Secure@1234"}' \
  -c cookies.txt
```

**Response (201 Created):**
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

---

### Login

```bash
curl -X POST http://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"alice@example.com","password":"Secure@1234"}' \
  -c cookies.txt
```

---

### Get wallet balance

```bash
export TOKEN="<accessToken from login>"

curl http://localhost:5002/api/wallet/balance \
  -H "Authorization: Bearer $TOKEN"
```

**Response (200 OK):**
```json
{
  "accountId": "...",
  "accountNumber": "SSW-0000000001",
  "currency": "USD",
  "balance": 0.00,
  "updatedAt": "2026-03-01T..."
}
```

---

### Refresh access token

```bash
curl -X POST http://localhost:5001/api/auth/refresh \
  -b cookies.txt -c cookies.txt
```

---

### Logout

```bash
curl -X POST http://localhost:5001/api/auth/logout \
  -H "Authorization: Bearer $TOKEN" \
  -b cookies.txt
```

**Response: 204 No Content** — refresh cookie is cleared.

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
