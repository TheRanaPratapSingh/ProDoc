# Admin operating guide

Platform admins need `admin.access` (and finer permissions as listed in `PermissionCodes`).

## Daily

- Review `/admin` overview metrics (appointments, queues, finance totals — actual DB aggregates)
- Triage support tickets
- Check verification queue for doctors/clinics
- Review growth dashboard for coupon/referral anomalies

## Verification

1. Open verification queue in the mobile admin / reviews flow
2. Confirm registration documents match submitted profile
3. Approve or reject with reason (audited)

## Finance

- Commission rules under admin finance
- Settlements for a clinic period (creates invoice PDF)
- Export CSV when available
- Refunds: only for paid payments; gateway path for online

## Growth

- Campaigns: activate/deactivate; respect marketing consent
- Coupons: create with limits; deactivate abused codes
- Reward adjustments: audited; never edit balances directly in SQL

## Moderation

- Review reports → hide / restore / remove with reason

## Emergency switches (feature flags)

| Flag | Effect |
| --- | --- |
| `payments.online` | Gate online checkout UX/API usage expectations |
| `assistant.enabled` | Disable AI assistant |
| `growth.referrals` / `growth.coupons` | Pause growth features |
| `support.tickets` | Pause new tickets if needed |
| `platform.maintenance` | Client should show maintenance messaging |

Change flags via Admin → Config. Changes are audited.
