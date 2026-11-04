# ProDoc Phase 12 — Production Readiness Report

**Status:** Staging-ready preparation complete. **Not** authorized for production launch, live payments, or real patient data.

**Date:** 2026-11-04  
**Scope:** Phases 1–11 codebase audit, critical/high fixes, CI/CD, ops docs.

---

## 1. Prioritized audit

### Critical (fixed or gated)

| Finding | Action |
| --- | --- |
| Machine-specific SQL connection string in committed `appsettings.json` | Removed; empty base config; Development overrides locally |
| Auto-migrate outside Development | Migrate only in Development or when `ProDoc:MigrateOnStartup` / `PRODOC_MIGRATE=true` |
| Production with Development payment provider | Startup guard rejects `Payments:Provider=Development` in Production |
| Empty CORS / wildcard outside Development | Startup requires explicit origins; rejects `*` |
| Dev auth keys outside Development | Existing validators + ProductionGuards |

### High (fixed or mitigated)

| Finding | Action |
| --- | --- |
| Missing security headers / HSTS | `SecurityHeadersMiddleware` + `UseHsts` outside Development |
| No global rate limit; webhooks unlimited beyond auth policy | Global limiter + `webhooks` policy on payment webhook |
| Health check not split live/ready | `/health/live`, `/health/ready`, `/health` |
| SQL transient failures without retry | `EnableRetryOnFailure` + execution strategy around appointment hold/move transactions |
| README/docs claimed reviews unpaid/deferred | Docs updated for Phases 1–11 reality |

### Medium (documented / partial)

| Finding | Status |
| --- | --- |
| SMS `IOtpSender` required outside Development | Intentional gate; configure provider before staging SMS |
| Signature allow-list is not malware scanning | Documented; integrate AV before handling untrusted uploads at scale |
| Web refresh token in `sessionStorage` | Known XSS exposure; HttpOnly cookie delivery deferred |
| Local disk document storage | Not suitable for multi-instance prod; use blob storage before HA |
| Background jobs (reminders, erasure, settlements sweeper) | Not registered; campaigns/opt-in hooks exist without schedulers |
| Refund endpoint authz is service-level ownership checks | Authenticated + ownership; finer permission attribute optional |
| Architecture doc still partially stale | Updated in this phase; keep in sync per release |

### Low

| Finding | Status |
| --- | --- |
| Swagger only in Development | Acceptable |
| Limited frontend automated E2E (Detox/Playwright) | Typecheck + small unit tests; manual/staging E2E required |
| Expo native release pipelines (EAS) | Documented, not automated in CI yet |
| Load testing beyond concurrent slot holds | Concurrent hold test added; broader k6 suite deferred |

---

## 2. What already looked solid

- JWT validation, refresh rotation / reuse detection, OTP rate limits
- Permission catalog seeding and `[HasPermission]` on sensitive routes
- Razorpay webhook HMAC verification when configured
- Payment idempotency keys; clinic vs online channels
- Medical record access gated by patient/treating relationship (admin alone insufficient)
- Upload signature checks; files outside public web root
- Serilog request logging without OTP/body dumping by default
- Filtered unique appointment slot index (booking conflict path tested)

---

## 3. Acceptance criteria mapping

| Criterion | Result |
| --- | --- |
| Critical/high security issues resolved or gated | Met for audited items |
| Essential workflows pass integration tests | Existing Phase 8–11 + concurrency tests |
| No duplicate appointments under tested concurrency | `ConcurrencyFlowTests` |
| Payment verify + webhook secure | Signature required for Razorpay; Development provider blocked in Production |
| Medical records protected | Existing access rules retained |
| Backups/restoration verified | **Procedures documented; operator must run restore drill on staging** |
| Monitoring/alerting work | Structured logs + health endpoints; external APM/uptime must be wired by ops |
| Staging deployment succeeds | Compose + Dockerfile provided; **do not treat as production** |
| Android/iOS/web builds verified | Web typecheck in CI; native store builds require EAS credentials (not run here) |
| Limitations documented | This file + `COMPLIANCE_GAPS.md` + launch checklist |

---

## 4. Remaining risks (explicit)

1. **No production deploy performed.** Requires human approval.
2. **No live Razorpay / SMS / OpenAI** in this environment.
3. **Backup restore not executed** against a real staging DB in this session—run the drill before go-live.
4. **DPDP / clinical compliance** are legal assessments, not engineering certification.
5. **Multi-region HA, WAF, Key Vault rotation, and AV scanning** remain operational follow-ups.
