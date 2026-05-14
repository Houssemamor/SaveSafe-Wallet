# SaveSafe Wallet (SSW)

SaveSafe Wallet is a microservices-based wallet platform for local development and testing.
It includes .NET backend services, an Angular frontend, and supporting infrastructure (Kafka, Redis, Grafana, Loki) orchestrated with Docker Compose.

## Architecture

Core application services:
- Auth Service (.NET): authentication, JWT, admin bootstrap
- Wallet Service (.NET): wallet domain operations
- Payment Service (.NET): Stripe checkout + webhook handling
- AI Security Service (Python): consumes login events from Kafka
- Frontend (Angular + Nginx)

Supporting infrastructure:
- Redis: caching and shared state
- Kafka + Zookeeper: event streaming
- Firestore (Firebase): persistent storage for backend services
- Grafana + Loki: observability

## Service URLs (local)

- Frontend: http://localhost:4200
- Auth Service: http://localhost:5001
- Wallet Service: http://localhost:5002
- Payment Service: http://localhost:5003
- AI Security Service: http://localhost:5010
- Grafana: http://localhost:3000
- Loki: http://localhost:3100

## Tech Stack

- Backend: ASP.NET Core (.NET)
- Frontend: Angular
- Messaging: Apache Kafka
- Cache: Redis
- Database: Google Firestore
- Payment: Stripe
- Monitoring: Grafana + Loki
- Containerization: Docker + Docker Compose

## Prerequisites

- Docker + Docker Compose
- Git
- Stripe CLI (for local webhook testing)
- Firebase service account JSON for Firestore access

## Quick Start

1. Clone the repository.

```bash
git clone <repo-url>
cd SaveSafe-Wallet
```

2. Create local environment file.

```bash
cp .env.example .env
```

3. Edit `.env` and set at least:
- `FIRESTORE_PROJECT_ID`
- `FIRESTORE_CREDENTIALS_PATH` (default: `./secrets/firebase.json`)
- `JWT_SECRET_KEY`
- `INTERNAL_API_KEY`
- `STRIPE_SECRET_KEY`
- `STRIPE_PUBLISHABLE_KEY`
- `STRIPE_WEBHOOK_SECRET`

4. Put your Firebase service account file at `./secrets/firebase.json`.

5. Start the full stack.

```bash
docker compose up -d
```

6. Optional: follow logs.

```bash
docker compose logs -f payment-service wallet-service frontend
```

7. Open the app at http://localhost:4200.

## Stripe Webhook (Local)

Payment webhook endpoint:
- `http://localhost:5003/api/payment/webhook`

Start Stripe forwarding:

```bash
.\stripe listen --forward-to http://localhost:5003/api/payment/webhook
```

Then copy the printed `whsec_...` value into:
- `STRIPE_WEBHOOK_SECRET` in `.env`

Rebuild payment service after env update:

```bash
docker compose up -d --build payment-service
```

Test card:
- `4242 4242 4242 4242`

## Useful Commands

Start all services:

```bash
docker compose up -d
```

Rebuild one service:

```bash
docker compose up -d --build payment-service
```

Show logs:

```bash
docker compose logs -f payment-service
```

Stop everything:

```bash
docker compose down
```

## Security and Secrets

Never commit:
- `.env`
- `secrets/firebase.json`
- any secret/token/key

This repository `.gitignore` already excludes secret files and local environment files.

If a secret was exposed:
1. Rotate it immediately
2. Remove it from Git history
3. Force-push cleaned history if needed

## Final Push Checklist

Before your final push:
1. Verify no secrets are staged
2. Verify `.env` and `secrets/` are ignored
3. Ensure containers build and run
4. Ensure Stripe webhook flow works (if payment demo is required)
5. Commit with a clear message

Quick checks:

```bash
git status --short
docker compose ps
```

## Suggested Git Workflow

```bash
git add .
git commit -m "chore: finalize project documentation and setup"
git push
```

## Contributing

Use `feature/*` and `fix/*` branches and open PRs with:
- clear summary
- test steps
- impact notes

## Support

If setup or Stripe forwarding fails, open an issue with:
- logs
- failing command
- service name
