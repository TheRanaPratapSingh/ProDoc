# ProDoc

ProDoc is a multi-surface healthcare platform for India: patients book clinic visits, doctors and reception manage schedules and queues, and Super Admin operates clinics, verification, and growth tools.

This repository is the **source of truth** for the full stack (API + Expo mobile/web client).

## Product surfaces

| Surface | Audience |
| --- | --- |
| Patient | OTP sign-in, discovery, booking, visits, records, rewards, assistant |
| Doctor | Schedule, queue, consultations, prescriptions, follow-ups |
| Reception / clinic | Desk booking, walk-ins, calendar, clinic ops |
| Super Admin | Clinics, doctors, users, verification, support, developer portal |

## Technology stack

- **Backend:** .NET 10 ASP.NET Core Web API, EF Core, SQL Server, Clean Architecture (`Domain` / `Application` / `Infrastructure` / `Api`)
- **Mobile / web client:** Expo (React Native), Expo Router, TypeScript, TanStack Query, Zustand, React Hook Form, Zod
- **Realtime:** SignalR notifications (where enabled)
- **Payments:** Razorpay integration path (Development provider for local/staging)
- **CI:** GitHub Actions (build/test)

## Repository structure

```text
apps/mobile/                 Expo React Native app (all roles)
src/ProDoc.Api/              HTTP API host
src/ProDoc.Application/      Application services
src/ProDoc.Domain/           Domain model
src/ProDoc.Infrastructure/   EF Core, stores, integrations
tests/                       Unit and API integration tests
docs/                        Architecture, deployment, compliance notes
deploy/                      Staging container templates
```

## Prerequisites

- .NET 10 SDK
- SQL Server (LocalDB, Express, or Docker)
- Node.js 20+ and npm
- Optional: Expo Go / Android Studio / Xcode for device runs

## Backend – local setup

```powershell
# From repository root
dotnet restore ProDoc.slnx
dotnet build ProDoc.slnx
dotnet ef database update --project src/ProDoc.Infrastructure --startup-project src/ProDoc.Api
dotnet run --project src/ProDoc.Api --launch-profile http
```

- API default (Development): `http://localhost:5080`
- Swagger: Development only
- Configure connection string via `appsettings.Development.json` **or** environment / user secrets (preferred for machine-specific servers)

Never commit production connection strings, JWT keys, Razorpay secrets, or SMS/OpenAI keys.

## Mobile – local setup

```powershell
cd apps/mobile
copy .env.example .env
# Set EXPO_PUBLIC_API_URL to your API (LAN IP for a physical phone)
npm ci
npm start
```

Useful scripts: `npm test`, `npx tsc --noEmit`.

## Environment variables (high level)

| Area | Examples (names only) |
| --- | --- |
| Database | `ConnectionStrings__ProDoc` |
| Auth | `Auth__SigningKey`, `Auth__OtpHashKey` |
| CORS | `Cors__Origins__0` … |
| Payments | `Payments__Provider`, `Payments__RazorpayKeyId`, … |
| AI | `Ai__Provider`, `Ai__ApiKey` |
| Mobile | `EXPO_PUBLIC_API_URL` |
| API tests | `PRODOC_TEST_SQL_SERVER` (optional server host for integration tests) |

Templates: `apps/mobile/.env.example`, `deploy/.env.example`, empty placeholders in `src/ProDoc.Api/appsettings.json`.

## Testing

```powershell
dotnet test ProDoc.slnx
cd apps/mobile
npm test
npx tsc --noEmit
```

## Documentation

| Doc | Purpose |
| --- | --- |
| `docs/ARCHITECTURE.md` | System design |
| `docs/DEPLOYMENT.md` | Environments and secrets |
| `docs/PRODUCTION_READINESS.md` | Readiness audit |
| `docs/LAUNCH_CHECKLIST.md` | Go-live gate |
| `docs/COMPLIANCE_GAPS.md` | Legal/regulatory questions (not certification) |
| `docs/ProDoc_Store_Deployment_Readiness_Audit.md` | Play / App Store audit |
| `docs/GIT_WORKFLOW.md` | Branching and day-to-day Git |

## Git workflow (summary)

- `main` — stable integration branch
- `develop` — optional ongoing integration
- `feature/*`, `bugfix/*`, `hotfix/*` — short-lived work

```bash
git status
git pull
git switch -c feature/short-name
# … implement …
git add -A
git commit -m "Describe why the change exists."
git push -u origin HEAD
```

Open a pull request into `main`. Do not force-push `main`.

## Security

- Keep secrets in environment variables, user secrets, or a vault — not in Git.
- `.gitignore` excludes `.env`, local appsettings overrides, keys, and build artifacts.
- If a secret was ever committed historically, **rotate it** and treat the old value as compromised. Do not rewrite shared history without explicit team approval.
- Do not enable live payments or real patient data without the launch checklist and legal/ops sign-off.

## Contribution

1. Branch from the latest `main`.
2. Keep commits focused and descriptive.
3. Run build/tests before opening a PR.
4. Prefer pull requests for review over direct pushes to `main` once branch protection is enabled.

## Hard boundaries

- Not authorized for production patient data, live payments, or store submission until readiness gates are closed.
- ABDM / partner integrations may be sandbox-only — do not claim production certification without evidence.
- Tax/GST configuration is not a certified invoicing claim.
