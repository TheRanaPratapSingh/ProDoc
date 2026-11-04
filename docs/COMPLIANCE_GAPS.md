# Compliance gap assessment (India-focused)

**This is an engineering gap list, not legal advice and not a compliance certification.**  
Qualified counsel / DPO / clinical compliance professionals must review before production with real patient data.

## Personal data (DPDP Act, 2023 — applicable obligations)

| Topic | Current product state | Gap / question for counsel |
| --- | --- | --- |
| Lawful purpose notice | In-app copy is partial; no formal privacy notice UX | Publish privacy policy + purpose limitation text |
| Consent | Marketing opt-in stored; clinical/assistant consents exist | Map every processing purpose to consent or legitimate use |
| Cross-border transfer | Not designed | Confirm hosting region (prefer India) |
| Data principal rights | Deletion **request** revokes sessions; erasure workflow incomplete | Complete erasure job, export, correction flows |
| Children’s data | Not age-gated | Policy for minors / guardians |
| Breach notification | Ops runbook stub only | Formal incident + notify process |

## Healthcare / clinical

| Topic | State | Gap |
| --- | --- | --- |
| Provider verification | Registration docs + admin approval | Confirm against state council / NMC process expectations |
| Prescriptions | Versioned PDFs, doctor permission gated | Clinical liability, e-sign, retention schedule |
| Record access | Patient / treating doctor relationship | Audit completeness for every clinical read |
| Telemedicine | Product is offline/in-person oriented | Do not market as telemedicine without rules review |

## Payments / GST

| Topic | State | Gap |
| --- | --- | --- |
| Gateway | Razorpay integration path | PCI scope is gateway-hosted; confirm SAQ |
| Invoices | Configurable tax rules; default 0% | **Not** certified GST invoicing; CA review required |
| Settlements | Commission batches | Contractual clarity with clinics |

## Product policies required before launch

- [ ] Terms of service
- [ ] Privacy policy (DPDP-aligned)
- [ ] Clinic / doctor agreement
- [ ] Refund and cancellation policy
- [ ] Grievance officer / support contact
- [ ] Cookie / local storage disclosure (web)

## Engineering controls that support compliance

- Audit log actions (auth, payments, reviews, growth adjustments)
- Marketing opt-out default for promotional campaigns
- Account deletion request path
- No OTP/token logging by design
- Medical document access through authorized API

None of the above alone establishes legal compliance.
