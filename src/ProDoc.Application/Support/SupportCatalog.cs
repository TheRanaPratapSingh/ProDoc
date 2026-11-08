using ProDoc.Domain.Identity;

namespace ProDoc.Application.Support;

/// <summary>Role-aware Help &amp; Support topic catalog shared by all portals.</summary>
public static class SupportCatalog
{
    public static SupportCatalogDetails Build(string? primaryRole, string supportEmail, string supportPhone, bool liveAgentsAvailable)
    {
        var role = string.IsNullOrWhiteSpace(primaryRole) ? RoleNames.Patient : primaryRole.Trim();
        var topics = AllTopics()
            .Where(topic => topic.Roles.Count == 0 || topic.Roles.Contains(role, StringComparer.OrdinalIgnoreCase)
                || role is RoleNames.PlatformAdmin or RoleNames.OrganizationAdmin or RoleNames.ClinicAdmin)
            .ToArray();
        return new SupportCatalogDetails(topics, supportEmail, supportPhone, liveAgentsAvailable);
    }

    private static IReadOnlyList<SupportTopicDetails> AllTopics() =>
    [
        Topic(
            "booking",
            "Booking & Appointment Help",
            "बुकिंग और अपॉइंटमेंट सहायता",
            "Find answers about booking, confirmation, cancellation, rescheduling, and queue tokens.",
            "बुकिंग, पुष्टि, रद्द, पुनर्निर्धारण और कतार टोकन से जुड़े उत्तर पाएँ।",
            "booking",
            [RoleNames.Patient, RoleNames.Doctor, RoleNames.Receptionist, RoleNames.ClinicAdmin],
            [
                Issue("booking_failed", "Appointment booking failed", "अपॉइंटमेंट बुकिंग विफल", "AppointmentIssue", "High"),
                Issue("not_confirmed", "Appointment not confirmed", "अपॉइंटमेंट कन्फर्म नहीं हुआ", "AppointmentIssue", "High"),
                Issue("wrong_time", "Wrong appointment date or time", "गलत तारीख या समय", "AppointmentIssue", "Normal"),
                Issue("doctor_unavailable", "Doctor not available", "डॉक्टर उपलब्ध नहीं", "AppointmentIssue", "Normal"),
                Issue("cancellation", "Appointment cancellation", "अपॉइंटमेंट रद्दीकरण", "CancellationDispute", "Normal"),
                Issue("reschedule", "Appointment rescheduling", "अपॉइंटमेंट पुनर्निर्धारण", "AppointmentIssue", "Normal"),
                Issue("token_queue", "Token or queue-related issues", "टोकन या कतार संबंधी समस्या", "AppointmentIssue", "Normal")
            ]),
        Topic(
            "payments",
            "Payment & Billing Help",
            "भुगतान और बिलिंग सहायता",
            "Get help with failed payments, refunds, receipts, and duplicate charges.",
            "विफल भुगतान, रिफंड, रसीद और डुप्लिकेट चार्ज के लिए मदद पाएँ।",
            "payments",
            [RoleNames.Patient, RoleNames.Receptionist, RoleNames.ClinicAdmin, RoleNames.Doctor],
            [
                Issue("payment_failed", "Payment failed", "भुगतान विफल", "PaymentIssue", "High"),
                Issue("deducted_unconfirmed", "Amount deducted but booking not confirmed", "राशि कट गई लेकिन बुकिंग कन्फर्म नहीं", "PaymentIssue", "Urgent"),
                Issue("payment_pending", "Payment pending", "भुगतान लंबित", "PaymentIssue", "Normal"),
                Issue("duplicate_payment", "Duplicate payment", "डुप्लिकेट भुगतान", "PaymentIssue", "High"),
                Issue("refund_status", "Refund status", "रिफंड स्थिति", "RefundRequest", "Normal"),
                Issue("invoice_receipt", "Invoice or receipt issue", "इनवॉइस या रसीद समस्या", "PaymentIssue", "Low")
            ]),
        Topic(
            "doctor_clinic",
            "Doctor & Clinic Help",
            "डॉक्टर और क्लिनिक सहायता",
            "Profile, availability, location, and verification questions.",
            "प्रोफ़ाइल, उपलब्धता, स्थान और सत्यापन संबंधी प्रश्न।",
            "onboarding",
            [RoleNames.Patient, RoleNames.Doctor, RoleNames.ClinicAdmin, RoleNames.Receptionist],
            [
                Issue("doctor_profile", "Doctor profile issue", "डॉक्टर प्रोफ़ाइल समस्या", "ProviderComplaint", "Normal"),
                Issue("clinic_info", "Clinic information incorrect", "क्लिनिक जानकारी गलत", "ProviderComplaint", "Normal"),
                Issue("availability", "Doctor availability issue", "डॉक्टर उपलब्धता समस्या", "AppointmentIssue", "Normal"),
                Issue("clinic_contact", "Clinic location or contact issue", "क्लिनिक स्थान या संपर्क समस्या", "Other", "Low"),
                Issue("verification", "Verification-related queries", "सत्यापन संबंधी प्रश्न", "ProviderComplaint", "Normal")
            ]),
        Topic(
            "account",
            "Account & Profile Help",
            "खाता और प्रोफ़ाइल सहायता",
            "OTP, login, profile updates, and family booking help.",
            "OTP, लॉगिन, प्रोफ़ाइल अपडेट और परिवार बुकिंग सहायता।",
            "account",
            [],
            [
                Issue("otp_login", "OTP or login problems", "OTP या लॉगिन समस्या", "AccountAccess", "High"),
                Issue("profile_update", "Profile update issues", "प्रोफ़ाइल अपडेट समस्या", "AccountAccess", "Normal"),
                Issue("patient_details", "Incorrect patient details", "गलत मरीज़ विवरण", "AccountAccess", "Normal"),
                Issue("family_booking", "Family member booking issues", "परिवार सदस्य बुकिंग समस्या", "AppointmentIssue", "Normal"),
                Issue("account_access", "Account access problems", "खाता एक्सेस समस्या", "AccountAccess", "High")
            ]),
        Topic(
            "technical",
            "Technical Support",
            "तकनीकी सहायता",
            "App crashes, loading issues, errors, and notification problems.",
            "ऐप क्रैश, लोडिंग, त्रुटियाँ और सूचना समस्याएँ।",
            "faq",
            [],
            [
                Issue("app_not_opening", "App not opening", "ऐप नहीं खुल रहा", "TechnicalProblem", "High"),
                Issue("loading_freeze", "Screen loading or freezing", "स्क्रीन लोड/फ्रीज़", "TechnicalProblem", "Normal"),
                Issue("error_messages", "Error messages", "त्रुटि संदेश", "TechnicalProblem", "Normal"),
                Issue("notifications", "Notification not received", "सूचना नहीं मिली", "TechnicalProblem", "Normal"),
                Issue("other_technical", "Other technical issues", "अन्य तकनीकी समस्याएँ", "TechnicalProblem", "Normal")
            ]),
        Topic(
            "other",
            "Other Queries",
            "अन्य प्रश्न",
            "General questions, suggestions, and feedback.",
            "सामान्य प्रश्न, सुझाव और प्रतिक्रिया।",
            "faq",
            [],
            [
                Issue("general", "General questions", "सामान्य प्रश्न", "Other", "Low"),
                Issue("feedback", "Suggestions and feedback", "सुझाव और प्रतिक्रिया", "Other", "Low"),
                Issue("other_support", "Other support requests", "अन्य सहायता अनुरोध", "Other", "Normal")
            ])
    ];

    private static SupportTopicDetails Topic(
        string code,
        string titleEn,
        string titleHi,
        string bodyEn,
        string bodyHi,
        string helpCategoryCode,
        IReadOnlyList<string> roles,
        IReadOnlyList<SupportIssueTypeDetails> issues) =>
        new(code, titleEn, titleHi, bodyEn, bodyHi, helpCategoryCode, issues, roles);

    private static SupportIssueTypeDetails Issue(
        string code,
        string labelEn,
        string labelHi,
        string ticketCategory,
        string defaultPriority) =>
        new(code, labelEn, labelHi, ticketCategory, defaultPriority);
}
