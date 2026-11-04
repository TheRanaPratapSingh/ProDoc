# Monitoring and incident response

## Telemetry already in the API

- Serilog console sink with `Application` and `Environment` enrichers
- HTTP request logging (method, path, status, elapsed) — **no request bodies**
- User id claim (`sub`) in diagnostic context when present
- Health endpoints: `/health/live`, `/health/ready`, `/health`

Wire console logs to App Insights / CloudWatch / Datadog via the host (App Service log stream, container agent, or Serilog sink package).

## What never to log

- OTP codes, JWTs, refresh tokens
- Razorpay secrets, webhook raw bodies with card data
- Full phone numbers in free-text logs (prefer masked)
- Prescription text, diagnoses, allergies, document bytes

## Recommended alerts

| Signal | Condition | Severity |
| --- | --- | --- |
| `/health/ready` | Fail 2 consecutive checks | P1 |
| HTTP 5xx rate | >1% for 5 minutes | P1 |
| Payment webhook failures | Spike in 401/409 from provider verify | P1 |
| Auth OTP rate-limit | Sustained 429 on `/auth` | P2 |
| DB DTU/CPU | >80% for 10 minutes | P2 |
| Backup age | >36 hours | P1 |
| Queue hub disconnect storms | Unusual SignalR drop rate | P2 |

## Payment reconciliation

1. Daily compare `Payments` where `Status=Paid` vs Razorpay settlements.
2. Investigate `PaymentWebhookEvent` duplicates / missing captures.
3. Alert when paid payments lack receipts (`Invoices` patient receipt) for >1 hour.

## Notification failures

Campaign and queue notifications use `UserNotifications`. Monitor insert failures and consent opt-outs. Marketing sends must respect `MarketingConsents`.

## Incident response (short)

1. Acknowledge; set feature flags off if needed (`payments.online`, `assistant.enabled`, `growth.coupons`).
2. Capture correlation / `traceId` from problem details.
3. Do not paste secrets or PHI into tickets.
4. Restore from backup only with dual control.
5. Post-incident: root cause, patient impact assessment, legal notify if required.
