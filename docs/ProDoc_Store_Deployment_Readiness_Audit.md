# ProDoc – Store Deployment Readiness Audit

**Audit type:** Evidence-based repository + policy review (Phase 1 audit only)  
**Audit date:** 2026-11-08  
**Scope:** Google Play Store (Android) and Apple App Store (iOS) readiness  
**Method:** Inspect existing codebase, configs, docs, and measured Expo export artifacts. **No source/config/database/build-setting changes** were made as part of implementing product fixes. Temporary local Expo export folders (`.audit-export-android`, `.audit-export-ios`) were generated only to measure JS/asset sizes.  
**Important:** No signed production AAB or IPA was produced. App sizes below distinguish **measured JS export** from **unmeasured native store binaries**.

---

## 1. Executive Summary

| Surface | Status |
| --- | --- |
| **Google Play Store Readiness** | **NOT READY** |
| **Apple App Store Readiness** | **NOT READY** |
| **Overall Production Readiness** | **NOT READY** / **BLOCKED – EXTERNAL ACTION REQUIRED** (developer accounts, legal docs, signed store builds, production ops) |

### Why

ProDoc has a substantial multi-role Expo + ASP.NET Core codebase and internal staging-oriented docs (`docs/PRODUCTION_READINESS.md`, `docs/LAUNCH_CHECKLIST.md`, `docs/COMPLIANCE_GAPS.md`) that already mark the product as **not authorized for production patient data**. This audit confirms store submission is blocked by missing release plumbing (no `eas.json`, no Android `package` / iOS `bundleIdentifier`), insecure transport defaults (cleartext / ATS arbitrary loads), missing privacy policy & store metadata, incomplete account erasure vs Play/Apple deletion rules, and **no verified production native builds**.

### Direct answers

1. **Can ProDoc be deployed to Google Play today?** **No.**
2. **Can ProDoc be deployed to Apple App Store today?** **No.**
3. **Actual Android production AAB/APK size?** **NOT VERIFIED** — no AAB/APK generated.
4. **Actual iOS production IPA size?** **NOT VERIFIED** — no IPA/archive generated.
5. **Measured JS export sizes (not store binaries):** Android Hermes bundle **7.43 MB**; iOS Hermes bundle **7.22 MB**; Android export tree **~12.8 MB**; iOS export tree **~11.7 MB**.
6. **Highest rejection / block risks:** missing privacy policy URL; incomplete deletion/erasure; cleartext/ATS; missing app IDs & EAS production profile; health-apps Data Safety / declarations incomplete; no reviewer demo credentials; no signed store builds.

---

## 2. Phase 1 – Project Discovery (Evidence)

### Stack (confirmed)

| Layer | Evidence |
| --- | --- |
| Mobile | `apps/mobile` — Expo `~58.0.23`, RN `0.88.1`, Expo Router, TanStack Query, Zustand, RHF, Zod |
| Backend | `src/ProDoc.*` — ASP.NET Core / EF Core / SQL Server Clean Architecture |
| App model | **Single multi-surface app** (patient, doctor, reception/clinic, staff, super-admin) via roles/permissions — not separate store apps |
| Entry | `expo-router/entry`; splash/auth → role surfaces |
| Version | `app.json` / `package.json` → `1.0.0` |
| Native projects | **No** checked-in `android/` or `ios/` folders (managed Expo workflow) |
| EAS | **`eas.json` absent**; `eas` CLI present on auditor machine but unused for release |
| Store IDs | **`android.package` and `ios.bundleIdentifier` not set** in `app.json` |
| Push | `expo-notifications` + `UIBackgroundModes: remote-notification` |
| Maps | External Google Maps URLs via `Linking` (`src/utils/maps.ts`) — not Google Maps SDK |
| Location | Web `navigator.geolocation` on directory screens; **no `expo-location` / iOS location purpose strings in `app.json`** |
| Payments | Backend Razorpay path + mobile payment status / create flows; live provider gated by production guards |
| AI | Vaidya Assistant with consent/disclaimer APIs |
| ABDM | Sandbox / readiness adapters only — **not** production ABDM certification |

### Environments

- Mobile API URL: `EXPO_PUBLIC_API_URL` (`.env.example` documents LAN HTTP for devices).
- Default `resolveApiBaseUrl()` falls back to `http://…:5080` when unset (`client.ts`).
- Backend: Development / Staging / Production guards documented in `docs/DEPLOYMENT.md`.

### Existing internal readiness posture

Repo docs already state staging-oriented readiness and **explicit non-production** status. Launch checklist Critical items remain unchecked (privacy/ToS, live payments, SMS, backups, etc.).

---

## 3. Actual Application Size

### Official limits vs engineering targets

| Source | Rule | Notes |
| --- | --- | --- |
| [Play size limits](https://support.google.com/googleplay/android-developer/answer/9859372) | Base module compressed download ≤ **500 MB**; cumulative modules + install-time packs ≤ **4 GB** | Official maximums |
| Play UX guidance | Warns users when download **> 200 MB** on mobile data | Soft conversion impact, not hard fail below limits |
| Apple | No single small “must be under X MB” public hard cap comparable to legacy APK 100 MB; practical download/install sizes still affect conversion | Engineering target typically keep first download lean |
| Recommended engineering target (this audit) | Compressed first download **well under 100–150 MB** for a healthcare utility app | Target ≠ official max |

### Measured vs store binaries

| Metric | Android | iOS |
| --- | --- | --- |
| **Store build generated** | **No** | **No** |
| Build format | n/a (no AAB/APK) | n/a (no IPA/Archive) |
| Actual store file size | **NOT VERIFIED** | **NOT VERIFIED** |
| Estimated Play/App Store download size | **NOT VERIFIED** | **NOT VERIFIED** |
| Estimated installed size | **NOT VERIFIED** | **NOT VERIFIED** |
| **Expo export generated** | **Yes** (`.audit-export-android`) | **Yes** (`.audit-export-ios`) |
| Hermes JS bundle (`.hbc`) | **7.43 MB** | **7.22 MB** |
| Full export tree size | **~12.84 MB** (69 files) | **~11.67 MB** (64 files) |
| Size target (recommended) | < 100–150 MB download | < 100–150 MB download |
| Size status vs official max | **Cannot assess AAB** | **Cannot assess IPA** |
| Size status vs JS export | JS+assets alone already ~12–13 MB before native runtime | Same |

**Do not treat Hermes export size as AAB/IPA size.** Native Expo/RN runtimes, 64-bit ABIs, and native modules are not included until `eas build` (or equivalent) produces an AAB/IPA.

### Asset / dependency size findings (measured)

**`apps/mobile/assets` total ~3.45 MB** (largest files):

| Asset | Size |
| --- | --- |
| `assets/images/icon.png` | ~780 KB |
| `splash-screen.png` | ~370 KB |
| `logo-glow.png` | ~324 KB |
| `splash-icon.png` | ~314 KB |
| `login-healthcare-hero.jpg` | ~254 KB |
| Duplicate splash variants (`splash-screen.jpg` + `.png`) | present |
| Unused/template leftovers | `react-logo*.png`, `expo-badge*.png`, `tutorial-web.png` |

**Fonts:** Runtime loads 7 faces (Plus Jakarta 4 + Noto Devanagari 3). Expo Android export still packaged **many additional `.ttf` weights** from `@expo-google-fonts/*` packages (export listing showed full weight families). Optimization opportunity: ensure only used faces ship.

**No videos** found under mobile assets.

---

## 4. Google Play Compliance Checklist

Policy sources checked (current as of audit date):

- Target API: [Play Console Help – Target API](https://support.google.com/googleplay/android-developer/answer/11926878) — **from 31 Aug 2026**, new apps/updates must target **API 36** (Android 16). *Audit date is 8 Nov 2026 → this requirement is already in force for new submissions.*
- User Data / Account deletion: [User Data](https://support.google.com/googleplay/android-developer/answer/10144311), [Account deletion](https://support.google.com/googleplay/android-developer/answer/13327111)
- Data Safety: [Data safety](https://support.google.com/googleplay/android-developer/answer/10787469)
- Health: [Health Content and Services](https://support.google.com/googleplay/android-developer/answer/16679511)
- Size: [Optimize app size](https://support.google.com/googleplay/android-developer/answer/9859372)
- AAB required for new apps (Play App Signing)

| Requirement | Status | Evidence | Problem | Recommended fix | Priority |
| --- | --- | --- | --- | --- | --- |
| Developer account / identity verification | **NOT VERIFIED** | Outside repo | Account may not exist / not verified | Complete Play Console org/personal verification | P0 |
| Application ID (`applicationId`) | **FAIL** | `app.json` has no `android.package` | Cannot publish without unique ID | Set production package e.g. `com.…prodoc` | P0 |
| Signed production AAB | **FAIL** | No EAS profile / no AAB | Cannot upload | Create `eas.json` production profile + Play App Signing | P0 |
| Target API 36 (as of 31 Aug 2026) | **NOT VERIFIED** | No prebuild/AAB to inspect `targetSdkVersion` | Expo 58 *likely* recent, but unproven | `eas build` then verify `targetSdkVersion ≥ 36` | P0 |
| 64-bit native libs | **NOT VERIFIED** | No AAB | — | Confirm via bundletool after build | P1 |
| Debuggable disabled / release config | **NOT VERIFIED** | No release binary | — | Verify release AAB | P0 |
| Cleartext traffic disabled in production | **FAIL** | `android.usesCleartextTraffic: true` | Insecure; policy/security risk | Production profile: cleartext **false**; HTTPS only | P0 |
| No dev server dependency in release | **FAIL / RISK** | Default API falls back to HTTP LAN/emulator hosts | Misconfigured release can ship wrong API | Hard-require `EXPO_PUBLIC_API_URL` HTTPS in production builds | P0 |
| Privacy Policy URL (Play listing + in-app) | **FAIL** | Copy: “Privacy Policy and Terms will appear here when published…” | Required for Data Safety / health apps | Publish non-editable HTTPS policy; link in-app + Console | P0 |
| Data Safety form | **NOT READY** | Collects phone, health/clinical data, location (web), payments | Form not preparable without accurate declarations | Complete after privacy inventory | P0 |
| Account deletion in-app | **PARTIAL** | In-app “Request account deletion” → `/deletion-request` | API message: account **stays on record until erasure completes**; erasure incomplete per docs | Complete erasure; disclose retention; add **web** deletion URL | P0 |
| External web deletion URL | **FAIL** | Not found in repo/listing assets | Required by Play User Data policy when accounts exist | Publish deletion request web page | P0 |
| Health apps declaration | **NOT READY** | Medical records, prescriptions, assistant | Must declare health/medical functionality | Complete Health apps form; accurate disclaimers | P0 |
| Content rating / ads / audience | **NOT VERIFIED** | No Console project evidence | — | Complete IARC + ads=no (if true) | P1 |
| Feature graphic / screenshots / listing | **FAIL** | No store marketing pack in repo | Listing incomplete | Produce Play assets (icon already partial) | P0 |
| Reviewer access / demo account | **NOT VERIFIED** | Not documented for Play | Health apps often need login | Provide OTP demo path or test accounts | P0 |
| Foreground service / background location | **LIKELY N/A** | No FGS/background location SDK found | Web geolocation only | Re-check if native location added later | P2 |
| Push notification permission disclosure | **PARTIAL** | Notifications plugin present | Need accurate Data Safety + runtime UX | Document device ID / push token collection | P1 |

---

## 5. Apple App Store Compliance Checklist

Policy sources:

- [Upcoming Requirements](https://developer.apple.com/news/upcoming-requirements/) — **since 28 Apr 2026**, uploads need **Xcode 26 + iOS 26 SDK**
- [App Review Guidelines](https://developer.apple.com/app-store/review/guidelines/) — privacy policy, account deletion 5.1.1(v), health data rules
- [Account deletion guidance](https://developer.apple.com/help/app-review/guideline-reference/5-1-1-account-deletion/)
- [Privacy manifests](https://developer.apple.com/documentation/bundleresources/privacy-manifest-files)

| Requirement | Status | Evidence | Problem | Recommended fix | Priority |
| --- | --- | --- | --- | --- | --- |
| Apple Developer Program membership | **NOT VERIFIED** | Outside repo | — | Organization enrollment + D-U-N-S if org | P0 |
| Bundle Identifier | **FAIL** | Missing `ios.bundleIdentifier` | Cannot register App ID / ASC | Set stable Bundle ID | P0 |
| Distribution cert / profiles | **NOT VERIFIED / BLOCKED** | No EAS credentials in audit | — | Configure EAS credentials | P0 |
| Xcode 26 / iOS 26 SDK build | **NOT VERIFIED** | No IPA; Expo 58 typically OK per Expo guidance | Must prove with ASC upload | EAS production image with Xcode 26 | P0 |
| ATS / cleartext | **FAIL** | `NSAllowsArbitraryLoads: true`, `NSAllowsLocalNetworking: true` | High App Review / security risk | Production Info.plist: disallow arbitrary loads; HTTPS API only | P0 |
| Privacy Policy in ASC + in-app | **FAIL** | Placeholder about-legal copy | Guideline 5.1.1(i) | Publish + deep-link in Settings/About | P0 |
| App Privacy Details (nutrition labels) | **NOT READY** | Phone, health, payments, possibly location | Must match reality | Complete ASC privacy answers | P0 |
| Account deletion in-app | **PARTIAL** | Request deletion exists | Freezing/pending ≠ full deletion; erasure incomplete | Complete deletion of account + associated personal data (retain only what law requires, disclosed) | P0 |
| PrivacyInfo.xcprivacy / required-reason APIs | **NOT VERIFIED** | Managed Expo — manifests come from build | Must verify in archive | Inspect EAS iOS build output | P1 |
| Permission purpose strings | **GAP / RISK** | No `NSLocationWhenInUseUsageDescription` etc. in `app.json` | If native location/camera/photos added later, required; web geolocation may behave differently on native | Add purpose strings before any native sensitive APIs | P1 |
| ATT | **LIKELY N/A** | No ads/tracking SDK found | Confirm no IDFA use | Declare tracking=false if accurate | P2 |
| Screenshots / metadata | **FAIL** | No ASC listing pack in repo | Incomplete submission | Produce iPhone (+ iPad if `supportsTablet: true`) screenshots | P0 |
| Medical / health claims | **RISK** | Assistant + records | Must not overclaim diagnosis/treatment | Keep disclaimers; review marketing copy | P0 |
| Payments for clinical services | **RISK** | Razorpay for healthcare visits | Physical services often outside IAP; still must follow Apple payment rules for digital goods | Confirm no digital unlock via external pay that violates 3.1.1; document clinic fee model for Review | P1 |
| Demo account for Review | **NOT VERIFIED** | OTP-only auth | Reviewers need a path | Provide test phone/OTP or reviewer instructions | P0 |

---

## 6. Security and Privacy Findings

### Critical

1. **`usesCleartextTraffic: true` (Android) and `NSAllowsArbitraryLoads: true` (iOS)** in `app.json` — unacceptable for store production builds.
2. **No published Privacy Policy / Terms** — store + DPDP readiness blocker.
3. **Account deletion is a request + session revoke; full erasure incomplete** (`AccountController` Accepted message; `COMPLIANCE_GAPS.md`). Conflicts with Play/Apple expectation to delete associated user data (with disclosed legal retention only).
4. **Production store builds / signing / app IDs missing** — cannot ship.
5. **Default API base URL is HTTP localhost/LAN** if env unset — dangerous for mis-built release.

### High

6. Health/clinical data (records, prescriptions) + AI assistant — Data Safety / App Privacy / Health declarations must be accurate; legal review required (DPDP, clinical liability).
7. SMS OTP / Razorpay / OpenAI keys — production secrets must be vaulted; Development OTP exposure blocked outside Development (good), but live SMS not verified here.
8. Document storage on local disk called out as non-HA in readiness docs.
9. Background erasure / retention jobs incomplete.
10. No mobile crash reporting SDK (Sentry/Crashlytics/etc.) found — production incident visibility weak.
11. Web refresh token in `sessionStorage` (XSS risk) — documented; native uses SecureStore (good).

### Medium

12. Large / duplicate image assets; possible unused font faces in export.
13. Geolocation via web API without native purpose strings — clarify native behavior.
14. Push token registration — ensure no PHI in push payloads (needs payload audit before launch).
15. Multi-role single app increases Review complexity (patient + doctor + admin in one binary).

### Low

16. Template Expo images still in assets.
17. Long branded splash (5.5–10s) — UX, not a store blocker.

### Legal (not auto-certified)

- DPDP Act 2023 obligations, CERT-In incident process, clinic agreements, GST/invoicing — **require qualified counsel**. Engineering controls alone ≠ compliance.
- **ABDM:** sandbox readiness only — **do not claim ABDM compliance**.

---

## 7. Functional Testing Results

**Runtime device E2E was not executed in this audit.** Status below is **code/API presence evidence**, not “passed in production.”

| Area | Result | Notes |
| --- | --- | --- |
| Patient OTP login UI + API | **Code present / NOT RUNTIME-TESTED** | Auth Result paths exist |
| Profile completion | **Code present** | |
| Find doctors/clinics | **Code present** | |
| Maps open external | **Code present** | Google Maps URLs |
| Booking / slots / queue | **Code present** + backend tests historically | Not re-run end-to-end here |
| Cancel / reschedule / family / share | **Code present** | |
| Follow-ups / notifications | **Code present** | Push registration code exists |
| Records / reviews / rewards / support / assistant | **Code present** | Assistant has consent + disclaimer |
| Account deletion request | **Code present** | Erasure incomplete |
| Doctor / reception / admin surfaces | **Code present** | Permission-gated routes |
| Unauthorized admin access | **Designed via permissions** | **NOT RUNTIME-TESTED** this audit |
| Live Razorpay / SMS / production API | **BLOCKED / NOT VERIFIED** | |
| Store listing flows | **FAILED (missing)** | |

**Summary:** Passed (runtime): **0** verified in this audit. Failed/missing for store: listing, privacy, IDs, signed builds, cleartext. Many product flows: **not-tested** on device.

---

## 8. Performance and Stability (Audit Notes)

| Topic | Evidence | Status |
| --- | --- | --- |
| Startup | Branded splash min 5.5s / max 10s | May feel slow; not a store hard fail |
| Query caching | React Query staleTime 60s; limited refetch | Positive |
| Unhandled rejections | Guard installed | Positive |
| Error boundary | Present | Positive |
| Crash reporting | Not found | Gap |
| Offline | Offline guards for some actions | Partial |
| Device lab testing | Not performed | NOT VERIFIED |

---

## 9. Build and Release Readiness

### Android

| Item | Status |
| --- | --- |
| EAS production profile | **Missing** (`eas.json` absent) |
| `android.package` | **Missing** |
| `versionCode` | **Missing** (not in `app.json`) |
| Signed AAB | **Not generated** |
| Play App Signing | **NOT VERIFIED** |
| Production API HTTPS | **Not locked** |
| Cleartext off | **Fail** |

### iOS

| Item | Status |
| --- | --- |
| EAS production profile | **Missing** |
| `ios.bundleIdentifier` | **Missing** |
| `buildNumber` | **Missing** |
| Distribution signing | **NOT VERIFIED / BLOCKED** without Apple credentials |
| ATS hardened | **Fail** |
| ASC app record | **NOT VERIFIED** |

---

## 10. Release Blockers (must fix before submission)

1. Create and register **Android applicationId** and **iOS Bundle ID**.
2. Add **EAS (or equivalent) production profiles** and produce **signed AAB + IPA**.
3. Disable **cleartext / ATS arbitrary loads** in production builds; force **HTTPS** API.
4. Publish **Privacy Policy + Terms** (public HTTPS) and link in-app + store consoles.
5. Implement **complete account/data deletion** (in-app + **web URL** for Play) with disclosed legal retention; finish erasure workflow.
6. Complete **Play Data Safety + Health apps declaration** and **Apple App Privacy** accurately.
7. Prepare **store listing assets** (feature graphic, screenshots, descriptions, support URL, contact).
8. Provide **App Review / Play review access** (OTP demo strategy).
9. Verify **targetSdkVersion ≥ 36** on the uploaded AAB (policy in force since 31 Aug 2026).
10. Verify iOS build with **Xcode 26 / iOS 26 SDK**.
11. External: **Play + Apple developer accounts**, legal/DPO sign-off, production SMS/payments/hosting/backups per `LAUNCH_CHECKLIST.md`.

---

## 11. Recommended Fix Roadmap

### P0 – Critical release blockers

- App IDs + EAS production builds (AAB/IPA) + signing  
- HTTPS-only + remove cleartext/ATS arbitrary loads for release  
- Privacy Policy / Terms / grievance contacts  
- Account deletion completion + web deletion page  
- Store listings + reviewer access  
- Health / Data Safety / App Privacy declarations  
- Confirm API 36 + Xcode 26 SDK on artifacts  

### P1 – High priority

- Crash reporting + production logging/APM  
- Push payload PHI review  
- Permission purpose strings before native sensitive APIs  
- Asset/font slim-down; remove unused images  
- Durable blob storage; backup restore drill  
- Live Razorpay + SMS configuration under vault  

### P2 – Recommended

- Split admin tooling out of consumer binary (optional but cleaner for Review)  
- Performance: shorten splash; image compression  
- Expand automated E2E (Detox/Maestro)  

### P3 – Post-launch

- ABDM production only after NHA onboarding (do not claim now)  
- Advanced AV scanning on uploads  
- HttpOnly cookie auth for web  

---

## 12. Official Policy Cross-Reference (selected)

| Requirement | Official URL | Effective / noted | ProDoc status | Action |
| --- | --- | --- | --- | --- |
| Target API 36 for new apps/updates | https://support.google.com/googleplay/android-developer/answer/11926878 | From **31 Aug 2026** | NOT VERIFIED on binary | Build AAB; verify targetSdk |
| Play account deletion + web link | https://support.google.com/googleplay/android-developer/answer/13327111 | Current User Data policy | PARTIAL / FAIL web link | Complete erasure + web URL |
| Play Data Safety | https://support.google.com/googleplay/android-developer/answer/10787469 | Current | NOT READY | Inventory + declare |
| Play Health apps | https://support.google.com/googleplay/android-developer/answer/16679511 | Current | NOT READY | Declaration + privacy |
| Play size limits | https://support.google.com/googleplay/android-developer/answer/9859372 | Current | AAB size NOT VERIFIED | Generate AAB; optimize |
| Apple Xcode 26 / iOS 26 SDK | https://developer.apple.com/news/upcoming-requirements/ | Since **28 Apr 2026** | NOT VERIFIED | EAS with Xcode 26 |
| Apple privacy policy + account deletion | https://developer.apple.com/app-store/review/guidelines/ ; https://developer.apple.com/help/app-review/guideline-reference/5-1-1-account-deletion/ | Current | FAIL policy URL / PARTIAL deletion | Publish policy; finish deletion |
| Privacy manifests | https://developer.apple.com/documentation/bundleresources/privacy-manifest-files | Required-reason APIs since 2024 | NOT VERIFIED in archive | Inspect IPA/build |

---

## 13. Final Verdict

| Question | Answer |
| --- | --- |
| Deploy to Google Play **today**? | **No — NOT READY** |
| Deploy to Apple App Store **today**? | **No — NOT READY** |
| Actual Android production build size? | **NOT VERIFIED** (no AAB). Measured Android Hermes bundle **7.43 MB**; export tree **~12.8 MB**. |
| Actual iOS production build size? | **NOT VERIFIED** (no IPA). Measured iOS Hermes bundle **7.22 MB**; export tree **~11.7 MB**. |
| Missing requirements? | App IDs, EAS release, signed binaries, hardened network security, privacy/legal pages, complete deletion, store metadata, consoles declarations, production ops/secrets |
| Likely rejection causes? | Missing/inaccurate privacy; incomplete deletion; ATS/cleartext; incomplete health disclosures; broken/demo login; incomplete listing; overclaiming medical/AI capabilities |
| Must fix before submission? | All **P0** items in §11 |
| External actions pending? | Play + Apple developer verification; legal/DPO; production hosting/SMS/payments; store Console setup; signed credential custody |

---

## Appendix A – Discovery inventory (roles)

| Role surface | Route/feature evidence | Store note |
| --- | --- | --- |
| Patient | home, booking, visits, records, rewards, assistant, about | Primary consumer listing |
| Doctor | doctor-* screens, portal APIs | Same binary |
| Reception / clinic | reception-* screens | Same binary |
| Super Admin | admin screens, create clinic/doctor | Same binary — Review complexity |

## Appendix B – Measurement commands used

```text
# Asset sizes under apps/mobile/assets
# Expo JS export (not AAB/IPA):
npx expo export --platform android --output-dir .audit-export-android
npx expo export --platform ios --output-dir .audit-export-ios
```

**No `eas build` / Xcode archive / bundletool AAB inspection was possible** without store credentials and production profiles.

---

*End of audit. Implementation of fixes is out of scope for this phase.*
