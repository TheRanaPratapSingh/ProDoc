using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ProDoc.Domain.Access;
using ProDoc.Domain.Admin;
using ProDoc.Domain.Appointments;
using ProDoc.Domain.Automation;
using ProDoc.Domain.Assistant;
using ProDoc.Domain.Billing;
using ProDoc.Domain.Clinical;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Enterprise;
using ProDoc.Domain.Growth;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Integrations;
using ProDoc.Domain.Mobile;
using ProDoc.Domain.Support;
using ProDoc.Domain.Patients;
using ProDoc.Domain.Providers;
using ProDoc.Domain.Queue;
using ProDoc.Domain.Reviews;
using ProDoc.Domain.Partners;
using ProDoc.Domain.SaaS;
using ProDoc.Domain.Scheduling;
using ProDoc.Infrastructure.Identity;

namespace ProDoc.Infrastructure.Persistence;

public sealed class ProDocDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public ProDocDbContext(DbContextOptions<ProDocDbContext> options)
        : base(options)
    {
    }

    public DbSet<Patient> Patients => Set<Patient>();

    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<Specialization> Specializations => Set<Specialization>();

    public DbSet<Doctor> Doctors => Set<Doctor>();

    public DbSet<Clinic> Clinics => Set<Clinic>();

    public DbSet<ClinicBranch> ClinicBranches => Set<ClinicBranch>();

    public DbSet<DoctorClinicAffiliation> DoctorClinicAffiliations => Set<DoctorClinicAffiliation>();

    public DbSet<ProviderDocument> ProviderDocuments => Set<ProviderDocument>();

    public DbSet<ConsultationSchedule> ConsultationSchedules => Set<ConsultationSchedule>();

    public DbSet<BranchOperatingHour> BranchOperatingHours => Set<BranchOperatingHour>();

    public DbSet<BranchClosure> BranchClosures => Set<BranchClosure>();

    public DbSet<ClinicFacility> ClinicFacilities => Set<ClinicFacility>();

    public DbSet<DoctorFavourite> DoctorFavourites => Set<DoctorFavourite>();

    public DbSet<Appointment> Appointments => Set<Appointment>();

    public DbSet<FamilyMember> FamilyMembers => Set<FamilyMember>();

    public DbSet<ClinicStaffMember> ClinicStaffMembers => Set<ClinicStaffMember>();

    public DbSet<ClinicQueueSettings> ClinicQueueSettings => Set<ClinicQueueSettings>();

    public DbSet<ClinicQueueSession> ClinicQueueSessions => Set<ClinicQueueSession>();

    public DbSet<QueueTokenCounter> QueueTokenCounters => Set<QueueTokenCounter>();

    public DbSet<QueueEntry> QueueEntries => Set<QueueEntry>();

    public DbSet<QueueStatusHistory> QueueStatusHistory => Set<QueueStatusHistory>();

    public DbSet<AppointmentCheckInToken> AppointmentCheckInTokens => Set<AppointmentCheckInToken>();

    public DbSet<WalkInRecord> WalkInRecords => Set<WalkInRecord>();

    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

    public DbSet<PatientMedicalRecord> PatientMedicalRecords => Set<PatientMedicalRecord>();

    public DbSet<PatientAllergy> PatientAllergies => Set<PatientAllergy>();

    public DbSet<PatientMedicalCondition> PatientMedicalConditions => Set<PatientMedicalCondition>();

    public DbSet<PatientMedication> PatientMedications => Set<PatientMedication>();

    public DbSet<MedicalDocument> MedicalDocuments => Set<MedicalDocument>();

    public DbSet<MedicineCatalogEntry> MedicineCatalog => Set<MedicineCatalogEntry>();

    public DbSet<MedicalRecordAccessLog> MedicalRecordAccessLogs => Set<MedicalRecordAccessLog>();

    public DbSet<ConsultationRecord> ConsultationRecords => Set<ConsultationRecord>();

    public DbSet<ConsultationDiagnosis> ConsultationDiagnoses => Set<ConsultationDiagnosis>();

    public DbSet<Prescription> Prescriptions => Set<Prescription>();

    public DbSet<PrescriptionVersion> PrescriptionVersions => Set<PrescriptionVersion>();

    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();

    public DbSet<ClinicPaymentSettings> ClinicPaymentSettings => Set<ClinicPaymentSettings>();

    public DbSet<TaxRule> TaxRules => Set<TaxRule>();

    public DbSet<CommissionRule> CommissionRules => Set<CommissionRule>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<PaymentWebhookEvent> PaymentWebhookEvents => Set<PaymentWebhookEvent>();

    public DbSet<Refund> Refunds => Set<Refund>();

    public DbSet<CommissionSnapshot> CommissionSnapshots => Set<CommissionSnapshot>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<SettlementBatch> SettlementBatches => Set<SettlementBatch>();

    public DbSet<SettlementLine> SettlementLines => Set<SettlementLine>();

    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();

    public DbSet<SupportTicketNote> SupportTicketNotes => Set<SupportTicketNote>();

    public DbSet<PlatformSetting> PlatformSettings => Set<PlatformSetting>();

    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();

    public DbSet<AssistantConversation> AssistantConversations => Set<AssistantConversation>();

    public DbSet<AssistantMessage> AssistantMessages => Set<AssistantMessage>();

    public DbSet<AssistantConsent> AssistantConsents => Set<AssistantConsent>();

    public DbSet<AssistantUsageDaily> AssistantUsageDaily => Set<AssistantUsageDaily>();

    public DbSet<PatientReview> PatientReviews => Set<PatientReview>();

    public DbSet<ReviewResponse> ReviewResponses => Set<ReviewResponse>();

    public DbSet<ReviewReport> ReviewReports => Set<ReviewReport>();

    public DbSet<ReviewModerationEvent> ReviewModerationEvents => Set<ReviewModerationEvent>();

    public DbSet<FeedbackSubmission> FeedbackSubmissions => Set<FeedbackSubmission>();

    public DbSet<ReferralCode> ReferralCodes => Set<ReferralCode>();

    public DbSet<ReferralRelationship> ReferralRelationships => Set<ReferralRelationship>();

    public DbSet<RewardAccount> RewardAccounts => Set<RewardAccount>();

    public DbSet<RewardTransaction> RewardTransactions => Set<RewardTransaction>();

    public DbSet<Coupon> Coupons => Set<Coupon>();

    public DbSet<CouponRedemption> CouponRedemptions => Set<CouponRedemption>();

    public DbSet<MarketingConsent> MarketingConsents => Set<MarketingConsent>();

    public DbSet<MarketingCampaign> MarketingCampaigns => Set<MarketingCampaign>();

    public DbSet<CampaignDelivery> CampaignDeliveries => Set<CampaignDelivery>();

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();

    public DbSet<OrganizationSetting> OrganizationSettings => Set<OrganizationSetting>();

    public DbSet<BranchSettingOverride> BranchSettingOverrides => Set<BranchSettingOverride>();

    public DbSet<ConsultationResource> ConsultationResources => Set<ConsultationResource>();

    public DbSet<ResourceAllocation> ResourceAllocations => Set<ResourceAllocation>();

    public DbSet<OrganizationSubscription> OrganizationSubscriptions => Set<OrganizationSubscription>();

    public DbSet<SaaSPlan> SaaSPlans => Set<SaaSPlan>();

    public DbSet<SaaSPlanEntitlement> SaaSPlanEntitlements => Set<SaaSPlanEntitlement>();

    public DbSet<OrganizationBranding> OrganizationBrandings => Set<OrganizationBranding>();

    public DbSet<OrganizationFeatureOverride> OrganizationFeatureOverrides => Set<OrganizationFeatureOverride>();

    public DbSet<SaaSSubscriptionCharge> SaaSSubscriptionCharges => Set<SaaSSubscriptionCharge>();

    public DbSet<AutomationWorkflow> AutomationWorkflows => Set<AutomationWorkflow>();

    public DbSet<AutomationOutboxEvent> AutomationOutboxEvents => Set<AutomationOutboxEvent>();

    public DbSet<AutomationWorkflowRun> AutomationWorkflowRuns => Set<AutomationWorkflowRun>();

    public DbSet<UserNotificationPreference> UserNotificationPreferences => Set<UserNotificationPreference>();

    public DbSet<AutomationFollowUpTask> AutomationFollowUpTasks => Set<AutomationFollowUpTask>();

    public DbSet<PartnerOrganization> PartnerOrganizations => Set<PartnerOrganization>();

    public DbSet<PartnerApp> PartnerApps => Set<PartnerApp>();

    public DbSet<PartnerCredential> PartnerCredentials => Set<PartnerCredential>();

    public DbSet<PartnerWebhookEndpoint> PartnerWebhookEndpoints => Set<PartnerWebhookEndpoint>();

    public DbSet<PartnerWebhookDelivery> PartnerWebhookDeliveries => Set<PartnerWebhookDelivery>();

    public DbSet<PartnerApiUsageDaily> PartnerApiUsageDaily => Set<PartnerApiUsageDaily>();

    public DbSet<PartnerBooking> PartnerBookings => Set<PartnerBooking>();

    public DbSet<IntegrationProvider> IntegrationProviders => Set<IntegrationProvider>();

    public DbSet<IntegrationConsent> IntegrationConsents => Set<IntegrationConsent>();

    public DbSet<IntegrationDeliveryLog> IntegrationDeliveryLogs => Set<IntegrationDeliveryLog>();

    public DbSet<IntegrationWebhookEvent> IntegrationWebhookEvents => Set<IntegrationWebhookEvent>();

    public DbSet<LabOrder> LabOrders => Set<LabOrder>();

    public DbSet<LabReport> LabReports => Set<LabReport>();

    public DbSet<PharmacyShare> PharmacyShares => Set<PharmacyShare>();

    public DbSet<AbhaLink> AbhaLinks => Set<AbhaLink>();

    public DbSet<PushDevice> PushDevices => Set<PushDevice>();

    public DbSet<HelpArticleCategory> HelpArticleCategories => Set<HelpArticleCategory>();

    public DbSet<HelpArticle> HelpArticles => Set<HelpArticle>();

    public DbSet<SupportAttachment> SupportAttachments => Set<SupportAttachment>();

    public DbSet<TicketStatusHistory> TicketStatusHistory => Set<TicketStatusHistory>();

    public DbSet<DisputeCase> DisputeCases => Set<DisputeCase>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("Users");
            entity.Property(user => user.SuspensionReason).HasMaxLength(240);
        });
        builder.Entity<IdentityRole<Guid>>().ToTable("Roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
        builder.ApplyConfigurationsFromAssembly(typeof(ProDocDbContext).Assembly);
    }
}
