# Deployment guide

## Environments

| Environment | Purpose | Migrate on startup | Payments | OTP |
| --- | --- | --- | --- | --- |
| Development | Local engineer machines | Yes | Development simulator | Exposed in API response |
| Staging | Pre-prod verification | Explicit flag only | Development or Razorpay test mode | SMS provider required if not Development host |
| Production | Live patients | **Never** auto-migrate without change control | Live Razorpay only | Real SMS; never expose OTP |

Set `ASPNETCORE_ENVIRONMENT` accordingly. Staging uses `appsettings.Staging.json` plus secret overrides.

## Required secrets (never commit)

- `ConnectionStrings__ProDoc`
- `Auth__SigningKey` (≥32 chars, unique per environment)
- `Auth__OtpHashKey` (≥32 chars, unique per environment)
- `Cors__Origins__0` … (exact SPA/app origins)
- `Payments__RazorpayKeyId` / `KeySecret` / `WebhookSecret` when Provider=Razorpay
- `Ai__ApiKey` when Provider=OpenAI

Recommended store: Azure Key Vault, AWS Secrets Manager, or GitHub Environments secrets.

## Recommended Azure footprint (estimate)

Indicative monthly USD for a small India launch (verify current pricing):

| Resource | SKU sketch | Rough estimate |
| --- | --- | --- |
| App Service (API) | B1 / P0v3 Linux | $13–55 |
| Azure SQL | Basic / S0 | $5–30 |
| Blob Storage (documents) | Hot LRS | $1–10 |
| Application Insights | Pay-as-you-go | $5–40 |
| Front Door / App Gateway (optional WAF) | Entry | $20–100+ |
| Communication SMS | Per message | Variable |

Total ballpark **$50–250/month** before SMS, Razorpay fees, and traffic spikes. Not a quote.

## Database migrations

```powershell
# Staging / production change window — take a backup first
dotnet ef database update --project src/ProDoc.Infrastructure --startup-project src/ProDoc.Api
```

Or set `ProDoc__MigrateOnStartup=true` **once** on a single staging instance, then unset. Do not enable continuous migrate in Production.

Rollback: restore from backup (see `BACKUP_RESTORE.md`). EF does not auto-generate down scripts for every migration; prefer restore.

## API container

```powershell
cd deploy
copy .env.example .env
# edit secrets
docker compose -f docker-compose.staging.yml up --build
```

Health:

- `GET /health/live` — process up
- `GET /health/ready` — SQL reachable
- `GET /health` — combined

## Mobile / web

```powershell
cd apps/mobile
$env:EXPO_PUBLIC_API_URL="https://staging-api.example.com"
npm run web
```

Native: configure EAS (`eas build --platform android|ios`) with production API URL. Do not ship Development OTP exposure.

## CI/CD

GitHub Actions workflow `.github/workflows/ci.yml`:

1. `dotnet build` + `dotnet test`
2. Mobile `tsc` + `npm test`
3. Docker image build (no push by default)

Add a **manual** `workflow_dispatch` deploy job later that targets staging only. Production deploy requires explicit approval gates.

## Rollback strategy

1. Keep previous App Service deployment slot / previous container tag.
2. Restore SQL from last verified backup if schema migration was applied.
3. Disable feature flags (`growth.*`, `payments.online`, `assistant.enabled`) if partial outage.
4. Communicate status; do not hot-fix production secrets into chat logs.
