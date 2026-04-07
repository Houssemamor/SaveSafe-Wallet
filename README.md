# SaveSafe Wallet (SSW)

Secure web-based banking & wallet platform with AI anti-pirating system.

**Stack**: .NET 9 (C# microservices) · PostgreSQL · Redis · Kafka · Grafana · Loki · Angular (Sprint 2+)

**Team**: AMOR Houssem · KHEDHIRI Montaha · BEN SLIMA Wissem · BAHROUNI Seif
**Supervisor**: Mme. Ines AGREBI

---

## Sprint 1 — What's Running

| Service | Port | Description |
|---|---|---|
| Frontend (Angular) | 4200 | login/register/dashboard/profile/wallet history |
| Auth Service | 5001 | register, login, refresh, logout · JWT · RBAC |
| Wallet Service | 5002 | wallet balance · ledger · internal provisioning |
| PostgreSQL | 5432 | `auth` and `wallet` schemas |
| Redis | 6379 | sessions, rate-limiting (Sprint 4) |
| Kafka + Zookeeper | 9092 | async messaging (Sprint 3) |
| Grafana | 3000 | dashboards (admin: `admin`/`admin`) |
| Loki | 3100 | centralized log aggregation |

---

## Prerequisites

- **Docker Desktop** ≥ 4.x (Docker Compose v2 included)
- **Git**

> .NET 9 SDK and PostgreSQL are **not** needed locally — everything runs in containers.
> Install .NET 9 SDK only if you want to run services outside Docker.

---

## Quick Start

```bash
# 1. Clone the repo
git clone <repo-url>
cd projectDev

# 2. Create your local .env (never commit this file)
cp .env.example .env
# Edit .env and set a strong JWT_SECRET_KEY (min 32 chars) and INTERNAL_API_KEY

# 3. Start the full stack
docker compose up -d

# 4. Watch logs (optional)
docker compose logs -f frontend auth-service wallet-service

# 5. Verify all services are healthy
docker compose ps
```

Wait ~30 seconds for Kafka to become ready. Then check:
- Frontend: http://localhost:4200
- Auth Service: http://localhost:5001/health
- Wallet Service: http://localhost:5002/health
- Swagger (Auth): http://localhost:5001/swagger (dev mode only)
- Grafana: http://localhost:3000 (login: admin / admin)

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
# Start only infrastructure
docker compose up postgres redis -d

# Auth Service
cd src/AuthService/AuthService.API
dotnet run

# Wallet Service (separate terminal)
cd src/WalletService/WalletService.API
dotnet run
```

The `appsettings.json` in each service is pre-configured to connect to `localhost:5432`.

---

## Database Migrations

EF Core migrations run **automatically on service startup** via `MigrateAsync()`.

To generate a new migration after schema changes:

```bash
# Auth Service
cd src/AuthService/AuthService.API
dotnet ef migrations add <MigrationName>

# Wallet Service
cd src/WalletService/WalletService.API
dotnet ef migrations add <MigrationName>
```

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
│   ├── AuthService/                    # .NET 9 WebAPI — auth, RBAC, JWT
│   └── WalletService/                  # .NET 9 WebAPI — wallet, ledger
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
