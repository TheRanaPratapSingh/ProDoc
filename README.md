# ProDoc

Offline doctor booking and clinic management platform (Phases 1–12 preparation).

## Stack

- .NET 10 modular monolith (Domain / Application / Infrastructure / API)
- SQL Server + EF Core migrations
- Expo React Native (web, Android, iOS)
- Serilog, health checks, GitHub Actions CI

## Run the API (Development)

```powershell
dotnet run --project src/ProDoc.Api --launch-profile http
```

API: `http://localhost:5080` · Swagger in Development only.

Development returns OTP as `developmentCode` and refuses to start outside Development until a real SMS `IOtpSender` is registered.

## Build and test

```powershell
dotnet build ProDoc.slnx
dotnet test ProDoc.slnx
cd apps/mobile
npm ci
npx tsc --noEmit
npm test
```

Migrations:

```powershell
dotnet ef database update --project src/ProDoc.Infrastructure --startup-project src/ProDoc.Api
```

## Staging container (no production patient data)

See `deploy/docker-compose.staging.yml` and `docs/DEPLOYMENT.md`.

## Documentation

| Doc | Purpose |
| --- | --- |
| `docs/ARCHITECTURE.md` | System design |
| `docs/PRODUCTION_READINESS.md` | Phase 12 audit + acceptance |
| `docs/DEPLOYMENT.md` | Environments, secrets, cloud sketch |
| `docs/BACKUP_RESTORE.md` | Backup / restore drill |
| `docs/MONITORING.md` | Logs, alerts, incidents |
| `docs/COMPLIANCE_GAPS.md` | Legal/regulatory questions (not certification) |
| `docs/LAUNCH_CHECKLIST.md` | Go-live gate |
| `docs/ADMIN_OPS.md` | Super-admin runbook |

## Phase coverage (high level)

1–4 Identity, providers, discovery, booking · 5 Queue · 6 Clinical · 7 Payments · 8 Admin · 9 Assistant · 10 Reviews · 11 Growth · 12 Production readiness (this phase)

## Hard boundaries

- Do **not** auto-publish to production, enable live payments, or migrate real patient data without explicit approval.
- Tax rules are configurable and are **not** a certified GST claim.
- Upload scanning is signature-based, not a malware-free guarantee.
