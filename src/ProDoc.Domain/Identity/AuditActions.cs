namespace ProDoc.Domain.Identity;

public static class AuditActions
{
    public const string OtpRequested = "auth.otp.requested";

    public const string SignedIn = "auth.signed_in";

    public const string SessionRefreshed = "auth.session.refreshed";

    public const string SignedOut = "auth.signed_out";

    public const string SignedOutAll = "auth.signed_out_all";

    public const string RefreshReuseDetected = "auth.refresh.reuse_detected";

    public const string ProfileUpdated = "account.profile_updated";

    public const string DeletionRequested = "account.deletion_requested";

    public const string DoctorSubmitted = "doctor.verification.submitted";

    public const string DoctorReviewed = "doctor.verification.reviewed";

    public const string ClinicSubmitted = "clinic.verification.submitted";

    public const string ClinicReviewed = "clinic.verification.reviewed";

    public const string AppointmentHeld = "appointment.held";

    public const string AppointmentConfirmed = "appointment.confirmed";

    public const string AppointmentRequested = "appointment.requested";

    public const string AppointmentApproved = "appointment.approved";

    public const string AppointmentRejected = "appointment.rejected";

    public const string AppointmentCancelled = "appointment.cancelled";

    public const string AppointmentRescheduled = "appointment.rescheduled";

    public const string AppointmentWalkIn = "appointment.walk_in";

    public const string AppointmentDeskBooked = "appointment.desk_booked";

    public const string AppointmentCheckedIn = "appointment.checked_in";

    public const string AppointmentShareCreated = "appointment.share.created";

    public const string AppointmentShareRevoked = "appointment.share.revoked";

    public const string AppointmentShareAccessed = "appointment.share.accessed";

    public const string DeskPatientRegistered = "reception.patient.registered";

    public const string DeskCollectionRecorded = "reception.billing.collected";

    public const string DeskRefundRequested = "reception.billing.refund_requested";

    public const string AppointmentCompleted = "appointment.completed";

    public const string ClinicStaffAssigned = "clinic.staff.assigned";

    public const string ClinicStaffRemoved = "clinic.staff.removed";

    public const string QueueEntryCreated = "queue.entry.created";

    public const string QueueEntryChanged = "queue.entry.changed";

    public const string ConsultationCreated = "clinical.consultation.created";

    public const string ConsultationUpdated = "clinical.consultation.updated";

    public const string ConsultationCompleted = "clinical.consultation.completed";

    public const string FollowUpCreated = "clinical.follow_up.created";

    public const string FollowUpUpdated = "clinical.follow_up.updated";

    public const string FollowUpCancelled = "clinical.follow_up.cancelled";

    public const string FollowUpCompleted = "clinical.follow_up.completed";

    public const string FollowUpLinked = "clinical.follow_up.linked";

    public const string PrescriptionDrafted = "clinical.prescription.drafted";

    public const string PrescriptionFinalized = "clinical.prescription.finalized";

    public const string PrescriptionAmended = "clinical.prescription.amended";

    public const string MedicalDocumentUploaded = "clinical.document.uploaded";

    public const string MedicalChartChanged = "clinical.chart.changed";

    public const string PaymentCreated = "billing.payment.created";

    public const string PaymentPaid = "billing.payment.paid";

    public const string PaymentFailed = "billing.payment.failed";

    public const string RefundRequested = "billing.refund.requested";

    public const string CommissionCaptured = "billing.commission.captured";

    public const string SettlementFinalized = "billing.settlement.finalized";

    public const string InvoiceIssued = "billing.invoice.issued";

    public const string AccountSuspended = "admin.account.suspended";

    public const string AccountReactivated = "admin.account.reactivated";

    public const string RoleGranted = "admin.role.granted";

    public const string RoleRevoked = "admin.role.revoked";

    public const string SupportTicketCreated = "support.ticket.created";

    public const string SupportTicketUpdated = "support.ticket.updated";

    public const string PlatformSettingChanged = "admin.config.changed";

    public const string FeatureFlagChanged = "admin.feature_flag.changed";

    public const string AssistantConversationStarted = "assistant.conversation.started";

    public const string AssistantConversationDeleted = "assistant.conversation.deleted";

    public const string AssistantMessageSent = "assistant.message.sent";

    public const string AssistantActionConfirmed = "assistant.action.confirmed";

    public const string AssistantConsentGranted = "assistant.consent.granted";

    public const string AssistantConsentRevoked = "assistant.consent.revoked";

    public const string ReviewCreated = "review.created";

    public const string ReviewUpdated = "review.updated";

    public const string ReviewResponseCreated = "review.response.created";

    public const string ReviewResponseUpdated = "review.response.updated";

    public const string ReviewReported = "review.reported";

    public const string ReviewModerated = "review.moderated";

    public const string FeedbackSubmitted = "feedback.submitted";

    public const string ReferralAttributed = "growth.referral.attributed";

    public const string RewardRedeemed = "growth.reward.redeemed";

    public const string RewardAdjusted = "growth.reward.adjusted";

    public const string CouponCreated = "growth.coupon.created";

    public const string CouponUpdated = "growth.coupon.updated";

    public const string MarketingConsentChanged = "growth.marketing.consent";

    public const string CampaignCreated = "growth.campaign.created";

    public const string CampaignUpdated = "growth.campaign.updated";

    public const string OrganizationCreated = "enterprise.organization.created";

    public const string OrganizationUpdated = "enterprise.organization.updated";

    public const string OrganizationClinicLinked = "enterprise.clinic.linked";

    public const string OrganizationMemberAssigned = "enterprise.member.assigned";

    public const string OrganizationMemberUpdated = "enterprise.member.updated";

    public const string OrganizationSettingChanged = "enterprise.setting.changed";

    public const string ResourceCreated = "enterprise.resource.created";

    public const string ResourceAllocated = "enterprise.resource.allocated";

    public const string OrganizationSubscriptionUpdated = "enterprise.subscription.updated";

    public const string IntegrationConsentGranted = "integrations.consent.granted";

    public const string IntegrationConsentRevoked = "integrations.consent.revoked";

    public const string IntegrationProviderConfigured = "integrations.provider.configured";

    public const string FhirBundleExported = "integrations.fhir.exported";

    public const string AbhaSandboxSimulated = "integrations.abha.sandbox_simulated";

    public const string AbhaLinkCleared = "integrations.abha.cleared";

    public const string LabOrderCreated = "integrations.lab.order_created";

    public const string PharmacyShareCreated = "integrations.pharmacy.share_created";

    public const string PharmacyShareWithdrawn = "integrations.pharmacy.share_withdrawn";

    public const string PushDeviceRegistered = "mobile.push.registered";

    public const string PushDeviceUnregistered = "mobile.push.unregistered";

    public const string HelpArticleUpdated = "support.help.updated";

    public const string SupportAttachmentAdded = "support.attachment.added";

    public const string DisputeOpened = "support.dispute.opened";

    public const string DisputeUpdated = "support.dispute.updated";

    public const string AnalyticsExported = "analytics.report.exported";

    public const string SaaSPlanConfigured = "saas.plan.configured";

    public const string TenantBrandingUpdated = "saas.tenant.branding_updated";

    public const string SaaSChargeCreated = "saas.charge.created";

    public const string SaaSChargePaid = "saas.charge.paid";

    public const string TenantSuspended = "saas.tenant.suspended";

    public const string TenantReactivated = "saas.tenant.reactivated";

    public const string TenantEntitlementChanged = "saas.tenant.entitlement_changed";

    public const string AutomationWorkflowChanged = "automation.workflow.changed";

    public const string NotificationPreferencesChanged = "automation.preferences.changed";

    public const string StaffInvited = "admin.staff.invited";

    public const string StaffInviteResent = "admin.staff.invite_resent";

    public const string StaffInviteAccepted = "admin.staff.invite_accepted";

    public const string StaffInviteCancelled = "admin.staff.invite_cancelled";

    public const string AdminClinicCreated = "admin.clinic.created";

    public const string AdminClinicUpdated = "admin.clinic.updated";

    public const string AdminClinicDoctorAssigned = "admin.clinic.doctor_assigned";

    public const string AdminClinicStaffAssigned = "admin.clinic.staff_assigned";

    public const string AdminUserDeleted = "admin.user.deleted";

    public const string ScheduleCreated = "schedule.created";

    public const string ScheduleUpdated = "schedule.updated";

    public const string ScheduleBookingModeChanged = "schedule.booking_mode_changed";

    public const string ScheduleHoursReplaced = "schedule.hours_replaced";

    public const string ScheduleBreaksReplaced = "schedule.breaks_replaced";

    public const string ScheduleClosureAdded = "schedule.closure_added";

    public const string ScheduleClosureRemoved = "schedule.closure_removed";

    public const string ScheduleExtraAdded = "schedule.extra_added";

    public const string ScheduleExtraRemoved = "schedule.extra_removed";

    public const string ScheduleDayOverrideReplaced = "schedule.day_override_replaced";

    public const string ScheduleDayCopied = "schedule.day_copied";

    public const string SchedulePublicationChanged = "schedule.publication_changed";

    public const string ScheduleDeleted = "schedule.deleted";
}
