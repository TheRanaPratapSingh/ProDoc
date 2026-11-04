# Architecture

ProDoc is a modular monolith. One ASP.NET Core API owns the business modules. The Expo application is the only client and runs on web, Android, and iOS.

## Decisions

**One deployable API.** Modules are namespaces inside Domain, Application, Infrastructure, and API. Separate services would add failure modes before the booking and queue boundaries are proven.

**OTP first.** Patients sign in with an Indian mobile number. The API stores an HMAC of the code, limits requests and attempts, and creates the patient on the first successful verification. The client cannot choose a role. A development-only phone allow-list can also grant Platform Admin.

**Refresh token rotation.** Refresh tokens are random, stored as SHA-256 hashes, and rotated on every refresh. Presenting a revoked token revokes the rest of that token family. Access tokens last 15 minutes and carry role and permission claims. A deleted account fails authorization even while the access token is unexpired.

**Permissions are code.** `PermissionCodes` is the catalog. Startup seeding adds roles and grants, and removes grants that the catalog no longer allows for the built-in roles. Endpoints opt in with `[HasPermission]`.

**Money waits; clinical records exist.** Commission rows will use `decimal`. Walk-in consultations are not platform revenue. Prescription publishing requires `prescriptions.publish` and an approved doctor. Finalized prescriptions are versioned and immutable; corrections create audited amendments. Clinical reads require the patient account (or dependent) or a treating-doctor care relationship—platform admin alone is not enough.

**SMS is an interface.** Development returns the code to the caller and does not log it. Production startup fails until a real `IOtpSender` is registered. This is intentional, not a silent fallback.

**Web token storage is a known limit.** Native builds will use the platform secure store. The web client keeps the refresh token in `sessionStorage`, which is exposed to script running on the page. HttpOnly cookie delivery is part of production hardening, not this phase.

**Deferred infrastructure.** SignalR ships with the queue. Background schedulers for reminders, account erasure completion, and settlement sweeps are still not registered; campaign helpers exist but are not timer-driven.

## Layout

```
src/ProDoc.Domain            entities and business rules
src/ProDoc.Application       use cases and ports
src/ProDoc.Infrastructure    SQL Server, Identity, JWT, OTP
src/ProDoc.Api               HTTP, validation, OpenAPI
apps/mobile                  Expo client
```

Application code depends on Domain. Infrastructure implements the ports. Controllers map request and response records and do not use EF types.

## Data

SQL Server, EF Core migrations, GUID keys, UTC `datetimeoffset`, and `rowversion` on patients, doctors, clinics, and consultation schedules. Identity tables are `Users`, `Roles`, and `UserRoles`. The database also stores patients, OTP challenges, refresh tokens, permissions, role grants, audit logs, specializations, doctor applications, clinic applications, branches, doctor-clinic affiliations, and private document metadata. Audit rows record the action, actor, time, IP address, and user agent. They do not record codes, tokens, document contents, or health information.

A doctor belongs to one user account. Signing in still assigns only Patient, plus Platform Admin for a development allow-list. The Doctor role is granted when an administrator approves the application. Clinic Admin is granted when that user's clinic is approved. A doctor may be affiliated with more than one clinic. Public search returns a doctor only when the application is approved, the doctor is accepting patients, and a current registration has not expired. The same gate applies to clinics. Changing a medical or clinic registration, or replacing a registration document, sends an approved profile back to Submitted and removes it from public search. The doctor cannot set their own status to Approved.

Provider files are stored outside the public web root. Downloads go through an authorized API route. The upload checker accepts PDF, JPEG, PNG, and WebP signatures and rejects executable extensions. Its engine name is `signature-allow-list`. It is the integration point for a malware scanner, and it does not report a file as free of malware. Submission writes an audit row and a log line with the application reference. Email and push notification are not configured.

A doctor keeps one consultation schedule for each clinic branch, with its own fee, visit length, buffer, capacity, effective dates, and time zone. Working hours, breaks, one-time sessions, and full-day closures are stored with that schedule. A clinic owner can also set branch opening hours, branch closures, and facility names. Open times are calculated in the clinic time zone for a date from today through 60 days and are clipped to branch hours when those hours exist. Public discovery returns a place only when the schedule is published, the doctor is approved, accepting patients, and not pending deletion, the current registration is unexpired, an affiliation exists, and the clinic branch is approved and active. Search can filter by name, specialty, qualification, city, locality, PIN code, fee, experience, gender, language, and availability. Sort options are experience, fee, distance, and earliest calculated slot. Public rating averages appear only after the configured minimum of verified published reviews. Map links use the configured URL template and do not embed an API key. Patient coordinates are used only for that search and are not stored. A doctor can edit their own schedules. The owner of that clinic can edit the schedule and the branch hours. Linking a doctor to a clinic is limited to a clinic that user owns or a clinic that is already publicly listed.

An appointment is a row for one doctor, branch, and start time. Held, requested, and confirmed rows share a filtered unique index so two patients cannot keep the same time. A hold lasts five minutes. Confirming it snapshots the schedule fee and either confirms the visit or leaves it requested when that schedule requires approval. The schedule setting wins; the patient cannot choose the mode. A requested visit expires at the earlier of four hours or thirty minutes before the start, and the time is released. The patient can cancel or reschedule until two hours before the start. The doctor and the clinic owner can do so until the start. The fee on the appointment does not change if the schedule fee changes later. Payment is pay at clinic. There is no payment capture and no SMS; confirmation is the appointment reference plus an audit row. Symptom notes stay on the appointment and are omitted from the administrator list and from logs. Family profiles belong to the account holder and can be used only by that account. A walk-in slot booking is created by the clinic owner. Desk walk-ins for the live queue are separate records that do not take a published slot. Clinic owners assign Receptionist or ClinicStaff accounts to a clinic (and optional branch) before those users can operate the desk. Check-in moves a confirmed appointment to CheckedIn and creates a queue entry with an atomic token. Completing consultation marks the appointment Completed. Queue status is separate from appointment status; skipping a patient does not cancel the appointment. QR check-in tokens are hashed, single-use, and bound to the appointment branch. Public waiting displays show tokens only.

## Phases

| Phase | Outcome | Done when |
| --- | --- | --- |
| 1 | Solution, identity, permissions, design system | OTP sign-in persists, tests cover rotation and deletion, client uses the API |
| 2 | Doctors, clinics, branches, verification | A doctor can belong to more than one clinic and is publicly listed only after approval |
| 3 | Discovery and schedules | Approved doctors with a published schedule can be found by name, specialty, clinic, city, fee, and location. Open times are calculated from the schedule |
| 4 | Appointment booking | Slot locks are transactional, booking is idempotent, approval and instant flows both exist |
| 5 | Clinic desk and live queue | Check-in and desk walk-ins create queue entries. Tokens are unique per doctor/branch/day. SignalR notifies; the database remains the source of truth |
| 6 | Prescriptions, records, family, notifications | Consultation workspace, versioned prescriptions with PDF, patient health records, secure document upload |
| 7 | Payments, commission, invoices, settlements | Online/dev gateway payments with server verification, commission snapshots on completed platform bookings (walk-ins excluded by default), receipts/settlements PDFs |
| 8 | Super Admin platform | Users, support, config flags, finance reports, audit |
| 9 | AI assistant | Grounded discovery/records help with consent and usage limits |
| 10 | Verified reviews | Appointment-bound ratings, moderation, public averages after threshold |
| 11 | Growth | Referrals, rewards ledger, coupons, consent-aware campaigns, analytics |
| 12 | Production readiness | Security guards, health split, CI/CD, deploy/backup/monitoring docs. SMS and HttpOnly web cookies remain deferred |

## Security notes

- Authorize every clinical read against the patient, the assigned doctor, or an explicit consent row.
- Keep document links short-lived.
- Validate uploaded file types before storage (signature allow-list; malware engine still optional).
- Do not log diagnoses, prescriptions, phone numbers in full, OTPs, or tokens.
- Account erasure is requested now and completed later. The request already revokes sessions.
- Outside Development: reject empty CORS, developer machine connection strings, Development payment provider in Production, and auto-migrate unless explicitly enabled.
