# SaveSafe Wallet (SSW)

Lightweight wallet platform (microservices) for local development and testing.

This repo contains services (Auth, Wallet, Payment) and an Angular frontend. The recommended way to run locally is with Docker Compose.

---

## Quick Overview
- Frontend: http://localhost:4200 (Angular)
- Auth Service: http://localhost:5001
- Wallet Service: http://localhost:5002
- Payment Service: http://localhost:5003

---

## Short Quick Start (recommended)

1. Clone the repo:

```bash
git clone <repo-url>
cd SaveSafe-Wallet
```

2. Copy example environment file and edit:

```bash
cp .env.example .env
# Edit .env: set FIRESTORE_PROJECT_ID, FIRESTORE_CREDENTIALS_PATH,
# JWT_SECRET_KEY, INTERNAL_API_KEY, STRIPE_SECRET_KEY and STRIPE_WEBHOOK_SECRET (see Stripe section).
```

3. Place your Firebase service account JSON at `./secrets/firebase.json` (or update `FIRESTORE_CREDENTIALS_PATH`).

4. Start services:

```bash
docker compose up -d
```

5. Watch logs (optional):

```bash
docker compose logs -f payment-service wallet-service frontend
```

6. Open the frontend: http://localhost:4200 and create an account or login.

---

## Stripe — Local Webhook (developer instructions)

Stripe webhooks must be forwarded to the running `payment-service` so that the backend can validate and credit wallets.

Recommended: use the Stripe CLI to forward events to `http://localhost:5003/api/payment/webhook`.

1. Install Stripe CLI: https://stripe.com/docs/stripe-cli  

ou with git repo https://github.com/stripe/stripe-cli/releases/tag/v1.40.9

2. Run:

```bash
\.stripe.exe listen --forward-to http://localhost:5003/api/payment/webhook
```

3. The CLI prints a `whsec_...` signing secret. Put that in your local `.env` as `STRIPE_WEBHOOK_SECRET`.

4. Use Stripe test keys in `.env` (never commit real secrets):

```text
STRIPE_SECRET_KEY=sk_test_...
STRIPE_PUBLISHABLE_KEY=pk_test_...
STRIPE_WEBHOOK_SECRET=whsec_...
```

5. Rebuild payment-service so it picks up `.env` changes:

```bash
docker compose up -d --build payment-service
```

6. Test end-to-end: create a top-up on the frontend and pay with the test card `4242 4242 4242 4242`.

Notes:
- Use only test keys locally. The publishable key may be public; keep secret key and webhook secret private.

---

## What to hide before pushing to GitHub

Do NOT commit any of the following to Git:

- `./.env` or any environment files containing secrets
- `./secrets/firebase.json` (service account JSON)
- any files containing `*_SECRET*`, `*_KEY*`, or credentials

This repository already contains a `.gitignore` that excludes `.env` and `secrets/`. Verify before pushing.

If you accidentally committed a secret, rotate it immediately and remove it from Git history (e.g., use `git rm --cached <file>` and force push after cleaning history).

---

## Pushing to GitHub (recommended workflow)

1. Initialize git (if not already) and create a repo on GitHub.

```bash
git init
git add .
git commit -m "Initial commit"
git remote add origin git@github.com:<your-org>/<repo>.git
git branch -M main
git push -u origin main
```

Before pushing, confirm `.gitignore` excludes secrets and your `.env` is not staged:

```bash
git status --short
```

If a secret is staged, unstage and remove it:

```bash
git reset HEAD <file>
git rm --cached <file>
```

---

## Useful commands

Start everything:

```bash
docker compose up -d
```

Build or rebuild a single service (example: payment-service):

```bash
docker compose up -d --build payment-service
```

Follow logs:

```bash
docker compose logs -f payment-service
```

Run Stripe CLI forward (example):

```bash
stripe listen --forward-to http://localhost:5003/api/payment/webhook
```

---

## Contributing

Please follow the branch strategy: use `feature/`, `fix/`, and open PRs against `develop` or `main` per your workflow. Provide a short description and test steps in PRs.

---

## Support

If you need help with local setup or Stripe forwarding, open an issue or ping the project maintainer.

---

`README.md` created to be concise and practical for developers onboarding locally.
