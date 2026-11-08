namespace ProDoc.Domain.Identity;

public sealed record PermissionDefinition(string Code, string Description);

public static class PermissionCodes
{
    public const string AccountRead = "account.read";
    public const string AccountWrite = "account.write";
    public const string AccountDeleteRequest = "account.delete.request";
    public const string DoctorsSearch = "doctors.search";
    public const string ProviderOnboard = "provider.onboard";
    public const string ClinicCreate = "clinic.create";
    public const string DoctorCreate = "doctor.create";
    public const string AppointmentsBookSelf = "appointments.book.self";
    public const string RecordsReadSelf = "records.read.self";
    public const string DoctorProfileWrite = "doctor.profile.write";
    public const string ScheduleManage = "schedule.manage";
    public const string AppointmentsManageAssigned = "appointments.manage.assigned";
    public const string PrescriptionsPublish = "prescriptions.publish";
    public const string ClinicManage = "clinic.manage";
    public const string StaffManage = "staff.manage";
    public const string QueueOperate = "queue.operate";
    public const string ReportsReadClinic = "reports.read.clinic";
    public const string AppointmentsBookWalkIn = "appointments.book.walkin";
    public const string AppointmentsManageClinic = "appointments.manage.clinic";
    public const string PatientsSearchClinic = "patients.search.clinic";
    public const string BillingCollectDesk = "billing.collect.desk";
    public const string RecordsCoordinateClinic = "records.coordinate.clinic";
    public const string AdminAccess = "admin.access";
    public const string VerificationReview = "verification.review";
    public const string CommissionsManage = "commissions.manage";
    public const string AuditRead = "audit.read";
    public const string AdminUsers = "admin.users";
    public const string AdminSupport = "admin.support";
    public const string AdminConfig = "admin.config";
    public const string AssistantUse = "assistant.use";
    public const string ReviewCreate = "review.create";
    public const string ReviewRespond = "review.respond";
    public const string ReviewModerate = "review.moderate";
    public const string GrowthUse = "growth.use";
    public const string GrowthManage = "growth.manage";
    public const string OrgAccess = "org.access";
    public const string OrgManage = "org.manage";
    public const string OrgStaff = "org.staff";
    public const string OrgAnalytics = "org.analytics";
    public const string OrgBilling = "org.billing";
    public const string IntegrationsUse = "integrations.use";
    public const string IntegrationsManage = "integrations.manage";
    public const string AnalyticsRead = "analytics.read";
    public const string AnalyticsExport = "analytics.export";
    public const string TenantsManage = "tenants.manage";
    public const string AutomationManage = "automation.manage";
    public const string PartnersManage = "partners.manage";
    public const string PartnersDevelop = "partners.develop";

    public static IReadOnlyList<PermissionDefinition> Catalog { get; } =
    [
        new(AccountRead, "Read the signed-in account."),
        new(AccountWrite, "Update the signed-in account profile."),
        new(AccountDeleteRequest, "Request erasure of the signed-in account."),
        new(DoctorsSearch, "Search published doctor profiles."),
        new(ProviderOnboard, "Update the signed-in user's existing doctor or clinic application (not create)."),
        new(ClinicCreate, "Create new clinic records. Restricted to Super Admin."),
        new(DoctorCreate, "Create new doctor profiles for users who are not already doctors. Restricted to Super Admin."),
        new(AppointmentsBookSelf, "Book an appointment for the signed-in patient."),
        new(RecordsReadSelf, "Read medical records shared with the signed-in patient."),
        new(DoctorProfileWrite, "Maintain the signed-in doctor's professional profile."),
        new(ScheduleManage, "Create and maintain doctor consultation schedules for owned or assigned clinic coverage."),
        new(AppointmentsManageAssigned, "Manage appointments assigned to the signed-in doctor."),
        new(PrescriptionsPublish, "Publish a prescription for a completed consultation."),
        new(ClinicManage, "Maintain clinic and branch profiles."),
        new(StaffManage, "Manage clinic staff access."),
        new(QueueOperate, "Operate the live clinic queue."),
        new(ReportsReadClinic, "Read clinic operational reports."),
        new(AppointmentsBookWalkIn, "Book a walk-in appointment at a clinic."),
        new(AppointmentsManageClinic, "Book, reschedule, and cancel clinic appointments for assigned doctors as staff."),
        new(PatientsSearchClinic, "Search patients who have a relationship with the clinic."),
        new(BillingCollectDesk, "Record pay-at-clinic collections at the reception desk."),
        new(RecordsCoordinateClinic, "View prescription readiness and upload patient reports for clinic visits (no Rx edit/sign)."),
        new(AdminAccess, "Open the platform administration portal."),
        new(VerificationReview, "Review doctor and clinic verification requests."),
        new(CommissionsManage, "Configure commission rules and settlements."),
        new(AuditRead, "Read the platform audit log."),
        new(AdminUsers, "Search accounts, manage roles, and suspend platform users."),
        new(AdminSupport, "Manage support tickets and complaints."),
        new(AdminConfig, "Edit platform settings and feature flags."),
        new(AssistantUse, "Use the ProDoc healthcare assistant for discovery and authorized record help."),
        new(ReviewCreate, "Submit verified appointment reviews for doctors and clinics."),
        new(ReviewRespond, "Respond to published patient reviews for owned doctor or clinic profiles."),
        new(ReviewModerate, "Moderate reported or inappropriate patient reviews."),
        new(GrowthUse, "Use referral codes, rewards wallet, and coupons."),
        new(GrowthManage, "Manage growth campaigns, coupons, and analytics."),
        new(OrgAccess, "Open organization dashboards for memberships."),
        new(OrgManage, "Manage organization clinics, branches, and settings."),
        new(OrgStaff, "Invite and manage organization staff memberships."),
        new(OrgAnalytics, "View organization analytics and branch comparisons."),
        new(OrgBilling, "Manage organization subscription billing configuration."),
        new(IntegrationsUse, "Manage personal healthcare integration consent, FHIR export, lab and pharmacy sandbox flows."),
        new(IntegrationsManage, "Configure healthcare integration providers and review integration readiness."),
        new(AnalyticsRead, "View platform business intelligence dashboards and aggregated operational analytics."),
        new(AnalyticsExport, "Export aggregated analytics reports as CSV or PDF without patient identifiers."),
        new(TenantsManage, "Create and manage SaaS tenants, plans, entitlements, and tenant suspensions."),
        new(AutomationManage, "Configure automation workflows, monitor executions, and view dead-letter queues."),
        new(PartnersManage, "Approve partner organizations, configure API scopes and quotas, and monitor partner API usage."),
        new(PartnersDevelop, "Register as a developer, manage partner applications, credentials, webhooks, and sandbox access.")
    ];

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> RoleGrants { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [RoleNames.Patient] =
            [
                AccountRead,
                AccountWrite,
                AccountDeleteRequest,
                DoctorsSearch,
                AppointmentsBookSelf,
                RecordsReadSelf,
                AssistantUse,
                ReviewCreate,
                GrowthUse,
                IntegrationsUse
                // ProviderOnboard / ClinicCreate / DoctorCreate / PartnersDevelop: Super Admin only.
            ],
            [RoleNames.Doctor] =
            [
                AccountRead,
                AccountWrite,
                ProviderOnboard,
                DoctorProfileWrite,
                ScheduleManage,
                AppointmentsManageAssigned,
                PrescriptionsPublish,
                ReviewRespond
            ],
            [RoleNames.ClinicAdmin] =
            [
                AccountRead,
                AccountWrite,
                ProviderOnboard,
                ClinicManage,
                StaffManage,
                QueueOperate,
                ReportsReadClinic,
                AppointmentsBookWalkIn,
                AppointmentsManageClinic,
                PatientsSearchClinic,
                BillingCollectDesk,
                RecordsCoordinateClinic,
                DoctorsSearch,
                ReviewRespond
            ],
            [RoleNames.Receptionist] =
            [
                AccountRead,
                AccountWrite,
                QueueOperate,
                AppointmentsBookWalkIn,
                AppointmentsManageClinic,
                PatientsSearchClinic,
                BillingCollectDesk,
                RecordsCoordinateClinic,
                DoctorsSearch,
                ReportsReadClinic,
                ScheduleManage
            ],
            [RoleNames.ClinicStaff] =
            [
                AccountRead,
                PatientsSearchClinic,
                ReportsReadClinic
            ],
            [RoleNames.OrganizationAdmin] =
            [
                AccountRead,
                AccountWrite,
                OrgAccess,
                OrgManage,
                OrgStaff,
                OrgAnalytics,
                OrgBilling,
                ClinicManage,
                StaffManage,
                QueueOperate,
                ReportsReadClinic,
                AppointmentsBookWalkIn,
                AppointmentsManageClinic,
                PatientsSearchClinic,
                BillingCollectDesk,
                RecordsCoordinateClinic,
                DoctorsSearch
            ],
            [RoleNames.PlatformAdmin] = Catalog.Select(permission => permission.Code).ToArray()
        };
}
