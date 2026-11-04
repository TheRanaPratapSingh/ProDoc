# Production launch checklist

**Do not launch until every Critical item is checked and stakeholders sign off.**

## Critical

- [ ] Explicit written approval to use production patient data
- [ ] Production secrets in vault (not git); rotated from Staging
- [ ] `Payments:Provider=Razorpay` with live keys + webhook secret
- [ ] Real `IOtpSender` SMS provider registered; `ExposeOtpInResponse=false`
- [ ] CORS origins locked to production apps only
- [ ] HTTPS + HSTS terminated correctly behind reverse proxy
- [ ] Database backup + **successful restore drill** recorded
- [ ] `/health/ready` monitored with paging
- [ ] Privacy policy, ToS, grievance contacts published
- [ ] Feature flags reviewed (`payments.online`, `assistant.enabled`, `growth.*`)

## High

- [ ] Staging E2E: register → book → pay → check-in → consult → prescription → review
- [ ] Concurrent booking / queue smoke under expected peak
- [ ] Razorpay webhook end-to-end in test then live
- [ ] Document storage on durable blob (not single-VM disk) if multi-instance
- [ ] Admin accounts MFA at IdP / host level
- [ ] Support ticket process staffed

## Medium

- [ ] EAS Android + iOS release builds signed
- [ ] Expo web deployed with correct `EXPO_PUBLIC_API_URL`
- [ ] Hindi/English and light/dark spot-checked on release build
- [ ] Application Insights (or equivalent) dashboards shared with on-call
- [ ] Runbook links in ops channel

## Explicit non-goals for day-0 (document as known limits)

- [ ] HttpOnly cookie auth for web (sessionStorage remains)
- [ ] Full AV malware scanning on uploads
- [ ] Automated background erasure completion
- [ ] Certified GST engine

## Sign-off

| Role | Name | Date | Approved |
| --- | --- | --- | --- |
| Engineering lead | | | |
| Security reviewer | | | |
| Product / ops | | | |
| Legal / DPO (if applicable) | | | |
