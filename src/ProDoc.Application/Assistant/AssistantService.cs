using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProDoc.Application.Abstractions;
using ProDoc.Application.Admin;
using ProDoc.Application.Appointments;
using ProDoc.Application.Clinical;
using ProDoc.Application.Providers;
using ProDoc.Application.Reviews;
using ProDoc.Application.Scheduling;
using ProDoc.Domain.Assistant;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;

namespace ProDoc.Application.Assistant;

public sealed class AssistantService(
    IAssistantStore store,
    IAdminStore adminStore,
    IAiProvider ai,
    DiscoveryService discovery,
    ScheduleService schedules,
    BookingService booking,
    MedicalService medical,
    IProviderStore providers,
    ReviewService reviewService,
    IClock clock,
    IOptions<AiOptions> options,
    ILogger<AssistantService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AssistantConsentDetails> GetConsentAsync(Guid userId, string? language, CancellationToken cancellationToken)
    {
        var lang = AssistantConversation.NormalizeLanguage(language);
        var consent = await store.FindConsentAsync(userId, false, cancellationToken);
        return MapConsent(consent, lang);
    }

    public async Task<AssistantConsentDetails> SetRecordsConsentAsync(
        Guid userId,
        bool allow,
        string? language,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var consent = await store.FindConsentAsync(userId, true, cancellationToken);
        if (consent is null)
        {
            consent = AssistantConsent.Create(userId, clock.UtcNow);
            store.AddConsent(consent);
        }

        consent.SetRecordsAccess(allow, clock.UtcNow);
        await store.AddAuditAsync(
            AuditLog.Record(userId, allow ? AuditActions.AssistantConsentGranted : AuditActions.AssistantConsentRevoked, clock.UtcNow, metadata.IpAddress, metadata.UserAgent),
            cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapConsent(consent, AssistantConversation.NormalizeLanguage(language));
    }

    public async Task<AssistantConversationDetails> StartConversationAsync(Guid userId, string? language, RequestMetadata metadata, CancellationToken cancellationToken)
    {
        await EnsureFeatureEnabledAsync(cancellationToken);
        var conversation = AssistantConversation.Start(userId, language ?? "en", clock.UtcNow);
        store.AddConversation(conversation);
        var welcome = conversation.Language == "hi"
            ? $"{AssistantSafetyCopy.DisclaimerHi}\n\nमैं डॉक्टर खोजने, अपॉइंटमेंट समझने और आपके अधिकृत रिकॉर्ड समझाने में मदद कर सकता हूँ। चिकित्सा निर्णय डॉक्टर से लें।"
            : $"{AssistantSafetyCopy.DisclaimerEn}\n\nI can help you find verified doctors, understand booking, and explain your own authorized records. Clinical decisions stay with a qualified doctor.";
        var message = conversation.AddMessage(AssistantMessageRole.Assistant, welcome, null, null, clock.UtcNow);
        store.AddMessage(message);
        await store.AddAuditAsync(Audit(userId, AuditActions.AssistantConversationStarted, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return await GetConversationAsync(userId, conversation.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<AssistantConversationSummary>> ListConversationsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await store.ListConversationsAsync(userId, 50, cancellationToken);
        return rows.Select(item => new AssistantConversationSummary(
            item.Id,
            item.Title,
            item.Language,
            item.UpdatedAtUtc,
            !string.IsNullOrWhiteSpace(item.PendingActionJson))).ToArray();
    }

    public async Task<AssistantConversationDetails> GetConversationAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken)
    {
        var conversation = await RequireConversationAsync(userId, conversationId, false, cancellationToken);
        return MapConversation(conversation);
    }

    public async Task DeleteConversationAsync(Guid userId, Guid conversationId, RequestMetadata metadata, CancellationToken cancellationToken)
    {
        var conversation = await RequireConversationAsync(userId, conversationId, true, cancellationToken);
        conversation.SoftDelete(clock.UtcNow);
        await store.AddAuditAsync(Audit(userId, AuditActions.AssistantConversationDeleted, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task<AssistantTurnResult> SendMessageAsync(
        Guid userId,
        Guid conversationId,
        string content,
        string? city,
        double? latitude,
        double? longitude,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        await EnsureFeatureEnabledAsync(cancellationToken);
        await EnsureRateLimitAsync(userId, cancellationToken);
        var conversation = await RequireConversationAsync(userId, conversationId, true, cancellationToken);
        var sanitized = AssistantSafety.SanitizeUserText(content);
        var userMessage = conversation.AddMessage(AssistantMessageRole.User, sanitized, null, null, clock.UtcNow);
        store.AddMessage(userMessage);

        var intent = AssistantSafety.ClassifyIntent(sanitized);
        var recommendations = new List<AssistantDoctorRecommendation>();
        AssistantPendingActionDetails? pending = null;
        var sources = new List<AssistantSourceRef>();
        var grounded = new StringBuilder();
        var isEmergency = intent == AssistantIntent.Emergency;
        string reply;

        var promptTokens = 0;
        var completionTokens = 0;
        if (intent == AssistantIntent.Emergency)
        {
            reply = conversation.Language == "hi" ? AssistantSafetyCopy.EmergencyHi : AssistantSafetyCopy.EmergencyEn;
            grounded.AppendLine("Emergency pathway triggered. No clinical tools executed.");
        }
        else if (intent == AssistantIntent.PromptInjection)
        {
            reply = conversation.Language == "hi"
                ? "मैं सिस्टम निर्देश नहीं बदल सकता। ProDoc सहायक केवल सुरक्षित नेविगेशन और आपके अधिकृत डेटा के आधार पर मदद करता है।"
                : "I cannot change system instructions. ProDoc Assistant only helps with safe navigation and your authorized data.";
            grounded.AppendLine("Prompt-injection pattern blocked. Tools not executed.");
        }
        else
        {
            if (intent is AssistantIntent.RecommendDoctor or AssistantIntent.BookAppointment or AssistantIntent.General)
            {
                recommendations.AddRange(await RecommendDoctorsAsync(sanitized, city, latitude, longitude, cancellationToken));
                if (recommendations.Count > 0)
                {
                    grounded.AppendLine("Verified doctor recommendations from ProDoc discovery (approved providers only):");
                    foreach (var item in recommendations.Take(5))
                    {
                        grounded.AppendLine($"- {item.DisplayName} | {item.Specialization} | fee ₹{item.ConsultationFee} | {item.City} | {item.Explanation}");
                        sources.Add(new AssistantSourceRef("doctor", item.DisplayName, item.DoctorId.ToString()));
                    }
                }
                else
                {
                    grounded.AppendLine("No matching verified doctors were found for the current filters.");
                }
            }

            if (intent == AssistantIntent.BookAppointment && recommendations.Count > 0)
            {
                var top = recommendations[0];
                var date = await NextOpenDateAsync(top.ScheduleId, cancellationToken);
                var slots = date is null
                    ? null
                    : await schedules.GetAvailabilityAsync(top.ScheduleId, date.Value, cancellationToken);
                var first = slots?.Slots.FirstOrDefault();
                if (date is not null && first is not null)
                {
                    var actionId = Guid.NewGuid();
                    var payload = new PendingActionPayload(
                        actionId,
                        nameof(AssistantPendingActionType.BookAppointment),
                        top.ScheduleId,
                        null,
                        date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        first.Start.ToString("HH:mm", CultureInfo.InvariantCulture),
                        top.DisplayName,
                        $"Book {top.DisplayName} on {date:yyyy-MM-dd} at {first.Start:HH:mm}");
                    conversation.SetPendingAction(JsonSerializer.Serialize(payload, JsonOptions), clock.UtcNow);
                    pending = MapPending(payload);
                    grounded.AppendLine($"Proposed booking requires explicit confirmation. scheduleId={top.ScheduleId}; date={date:yyyy-MM-dd}; start={first.Start:HH:mm}.");
                    grounded.AppendLine("Do not claim the appointment is booked until the patient confirms.");
                }
                else
                {
                    grounded.AppendLine("No open slots were available to propose. Ask the patient to pick another day in the booking screen.");
                }
            }

            if (intent is AssistantIntent.CancelAppointment or AssistantIntent.RescheduleAppointment)
            {
                grounded.AppendLine("Cancellation and reschedule require the patient to confirm in Visits using Phase 4 booking APIs. Assistant must not execute them silently.");
                var visits = await booking.ListMineAsync(userId, cancellationToken);
                foreach (var visit in visits.Where(item => item.Status is "Confirmed" or "Requested").Take(5))
                {
                    grounded.AppendLine($"Visit {visit.Reference} with {visit.DoctorName} on {visit.StartsAtUtc:u} status={visit.Status}");
                    sources.Add(new AssistantSourceRef("appointment", visit.Reference ?? visit.Id.ToString(), visit.Id.ToString()));
                }
            }

            if (intent == AssistantIntent.MedicalRecords)
            {
                var consent = await store.FindConsentAsync(userId, false, cancellationToken);
                if (consent is not { RecordsAccessAllowed: true })
                {
                    grounded.AppendLine("Records access blocked: patient has not granted AI records consent.");
                }
                else
                {
                    var records = await medical.ListMyRecordsAsync(userId, cancellationToken);
                    grounded.AppendLine("Authorized medical record facts for this patient only:");
                    foreach (var record in records.Take(3))
                    {
                        var details = await medical.GetRecordAsync(userId, record.Id, metadata, cancellationToken);
                        grounded.AppendLine($"Record {details.Id} subject={details.SubjectName}");
                        sources.Add(new AssistantSourceRef("medical_record", details.SubjectName, details.Id.ToString()));
                        foreach (var allergy in details.Allergies.Where(item => item.IsActive).Take(5))
                        {
                            grounded.AppendLine($"FACT allergy: {allergy.Substance} severity={allergy.Severity}");
                        }

                        foreach (var condition in details.Conditions.Where(item => item.IsActive).Take(5))
                        {
                            grounded.AppendLine($"FACT condition: {condition.Name}");
                        }

                        foreach (var prescription in details.Prescriptions.Take(3))
                        {
                            grounded.AppendLine($"FACT prescription {prescription.Number} status={prescription.Status} diagnosis={prescription.Diagnosis}");
                            sources.Add(new AssistantSourceRef("prescription", prescription.Number, prescription.Id.ToString()));
                        }
                    }

                    grounded.AppendLine("Distinguish recorded facts from general education. Do not change medicines or dosages.");
                }
            }

            if (intent is AssistantIntent.NavigationHelp or AssistantIntent.General or AssistantIntent.HealthEducation)
            {
                grounded.AppendLine("Navigation facts: Patients search doctors, hold a slot for 5 minutes, then confirm. Queue check-in is available after confirmation. Walk-ins are clinic-operated.");
                grounded.AppendLine("Health education must stay general, cite that it is not personal medical advice, and encourage doctor consultation for clinical judgment.");
                if (AssistantSafety.RequestsClinicalAutonomy(sanitized))
                {
                    grounded.AppendLine("User asked for diagnosis/prescription/dosage. Refuse autonomous clinical actions.");
                }
            }

            var completion = await ai.CompleteAsync(
                new AiCompletionRequest(
                    conversation.PromptVersion,
                    conversation.Language,
                    conversation.Messages
                        .OrderBy(item => item.CreatedAtUtc)
                        .TakeLast(12)
                        .Select(item => new AiChatMessage(item.Role.ToString().ToLowerInvariant(), item.Content))
                        .ToArray(),
                    grounded.ToString(),
                    [
                        conversation.Language == "hi" ? AssistantSafetyCopy.DisclaimerHi : AssistantSafetyCopy.DisclaimerEn,
                        conversation.Language == "hi" ? AssistantSafetyCopy.EmergencyHi : AssistantSafetyCopy.EmergencyEn
                    ]),
                cancellationToken);

            reply = string.IsNullOrWhiteSpace(completion.Content)
                ? FallbackReply(conversation.Language, intent, recommendations, pending)
                : completion.Content.Trim();
            promptTokens = completion.PromptTokens;
            completionTokens = completion.CompletionTokens;
        }

        await TrackUsageAsync(userId, promptTokens, completionTokens, cancellationToken);

        var assistantMessage = conversation.AddMessage(
            AssistantMessageRole.Assistant,
            reply,
            null,
            sources.Select(item => $"{item.Kind}:{item.Id}").ToArray(),
            clock.UtcNow);
        store.AddMessage(assistantMessage);
        await store.AddAuditAsync(Audit(userId, AuditActions.AssistantMessageSent, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Assistant turn user={UserId} conversation={ConversationId} intent={Intent}", userId, conversationId, intent);
        return new AssistantTurnResult(
            conversation.Id,
            MapMessage(assistantMessage, sources),
            pending ?? ParsePending(conversation.PendingActionJson),
            recommendations,
            isEmergency,
            conversation.Language == "hi" ? AssistantSafetyCopy.DisclaimerHi : AssistantSafetyCopy.DisclaimerEn);
    }

    public async Task<IReadOnlyList<AssistantDoctorRecommendation>> RecommendAsync(
        Guid userId,
        string? text,
        string? city,
        double? latitude,
        double? longitude,
        CancellationToken cancellationToken)
    {
        await EnsureFeatureEnabledAsync(cancellationToken);
        _ = userId;
        return await RecommendDoctorsAsync(text ?? string.Empty, city, latitude, longitude, cancellationToken);
    }

    public async Task<ConfirmAssistantActionResult> ConfirmPendingActionAsync(
        Guid userId,
        Guid conversationId,
        Guid actionId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        await EnsureFeatureEnabledAsync(cancellationToken);
        var conversation = await RequireConversationAsync(userId, conversationId, true, cancellationToken);
        var pending = ParsePending(conversation.PendingActionJson)
            ?? throw DomainException.Validation("assistant_action_missing", "There is no pending assistant action to confirm.");
        if (pending.ActionId != actionId)
        {
            throw DomainException.Validation("assistant_action_mismatch", "Confirm the latest proposed action.");
        }

        if (!string.Equals(pending.Type, nameof(AssistantPendingActionType.BookAppointment), StringComparison.Ordinal))
        {
            throw DomainException.Validation("assistant_action_unsupported", "Only booking proposals can be confirmed here.");
        }

        if (pending.ScheduleId is null || pending.Date is null || pending.Start is null
            || !DateOnly.TryParse(pending.Date, CultureInfo.InvariantCulture, out var date)
            || !TimeOnly.TryParse(pending.Start, CultureInfo.InvariantCulture, out var start))
        {
            throw DomainException.Validation("assistant_action_invalid", "The proposed booking details are incomplete.");
        }

        var held = await booking.HoldAsync(userId, pending.ScheduleId.Value, date, start, metadata, cancellationToken);
        var confirmed = await booking.ConfirmAsync(userId, held.Id, null, "Booked with ProDoc Assistant", null, metadata, cancellationToken);
        conversation.SetPendingAction(null, clock.UtcNow);
        var note = conversation.Language == "hi"
            ? $"अपॉइंटमेंट पुष्टि हो गई: {confirmed.Reference}."
            : $"Appointment confirmed: {confirmed.Reference}.";
        var message = conversation.AddMessage(AssistantMessageRole.Assistant, note, "booking.confirm", [confirmed.Id.ToString()], clock.UtcNow);
        store.AddMessage(message);
        await store.AddAuditAsync(Audit(userId, AuditActions.AssistantActionConfirmed, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return new ConfirmAssistantActionResult(conversation.Id, pending.Type, confirmed.Id, confirmed.Status, note);
    }

    private async Task<IReadOnlyList<AssistantDoctorRecommendation>> RecommendDoctorsAsync(
        string text,
        string? city,
        double? latitude,
        double? longitude,
        CancellationToken cancellationToken)
    {
        var specializationHint = AssistantSafety.InferSpecializationHint(text);
        Guid? specializationId = null;
        if (!string.IsNullOrWhiteSpace(specializationHint))
        {
            var catalog = await providers.ListSpecializationsAsync(cancellationToken);
            specializationId = catalog.FirstOrDefault(item =>
                item.Code.Contains(specializationHint, StringComparison.OrdinalIgnoreCase)
                || item.Name.Contains(specializationHint, StringComparison.OrdinalIgnoreCase))?.Id;
        }

        var places = await discovery.SearchAsync(
            new DiscoveryQuery(
                Text: null,
                SpecializationId: specializationId,
                ClinicId: null,
                City: string.IsNullOrWhiteSpace(city) ? null : city,
                Latitude: latitude,
                Longitude: longitude,
                RadiusKm: latitude is null ? null : 25,
                MinFee: null,
                MaxFee: null,
                MinExperience: null,
                Gender: null,
                Language: null,
                Qualification: null,
                PostalCode: null,
                Locality: null,
                Availability: "week",
                Sort: latitude is null ? "recommended" : "nearest",
                ClinicType: null,
                Page: 1,
                PageSize: 8),
            cancellationToken);

        var results = new List<AssistantDoctorRecommendation>();
        foreach (var place in places.Take(5))
        {
            var doctor = await discovery.GetDoctorAsync(place.DoctorId, cancellationToken);
            var reasons = new List<string>
            {
                "Verified ProDoc provider",
                $"Specialization: {doctor.PrimarySpecialization}"
            };
            if (place.City is not null)
            {
                reasons.Add($"Location: {place.City}");
            }

            if (place.DistanceKm is not null)
            {
                reasons.Add($"About {place.DistanceKm:0.0} km away");
            }

            reasons.Add($"Consultation fee ₹{place.ConsultationFee}");
            if (doctor.Languages.Count > 0)
            {
                reasons.Add($"Languages: {string.Join(", ", doctor.Languages)}");
            }

            reasons.Add("Availability filter: open slots this week");
            var rating = await reviewService.DoctorRatingSummaryAsync(place.DoctorId, cancellationToken);
            string? ratingNote;
            if (rating.AverageIsPublic && rating.AverageOverall is decimal average)
            {
                reasons.Add($"Verified rating {average:0.0} from {rating.PublishedReviewCount} reviews");
                ratingNote = rating.DisplayNote;
            }
            else
            {
                ratingNote = rating.DisplayNote;
            }

            results.Add(new AssistantDoctorRecommendation(
                place.DoctorId,
                place.ScheduleId,
                doctor.DisplayName,
                doctor.PrimarySpecialization,
                place.ClinicName,
                place.City,
                place.ConsultationFee,
                doctor.YearsOfExperience,
                doctor.Languages,
                place.DistanceKm,
                string.Join("; ", reasons),
                ratingNote));
        }

        return results;
    }

    private async Task<DateOnly?> NextOpenDateAsync(Guid scheduleId, CancellationToken cancellationToken)
    {
        var zone = TimeZoneInfo.TryFindSystemTimeZoneById("Asia/Kolkata", out var resolved)
            ? resolved
            : TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow, zone).DateTime);
        for (var offset = 1; offset <= 14; offset++)
        {
            var date = today.AddDays(offset);
            var availability = await schedules.GetAvailabilityAsync(scheduleId, date, cancellationToken);
            if (availability.Slots.Count > 0)
            {
                return date;
            }
        }

        return null;
    }

    private async Task EnsureRateLimitAsync(Guid userId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var usage = await store.FindUsageAsync(userId, today, false, cancellationToken);
        if (usage is not null && usage.RequestCount >= Math.Max(1, options.Value.MaxRequestsPerUserPerDay))
        {
            throw DomainException.TooManyRequests("assistant_rate_limited", "You have reached today's assistant limit. Try again tomorrow.");
        }
    }

    private async Task TrackUsageAsync(Guid userId, int promptTokens, int completionTokens, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var usage = await store.FindUsageAsync(userId, today, true, cancellationToken);
        if (usage is null)
        {
            usage = AssistantUsageDaily.Create(userId, today);
            store.AddUsage(usage);
        }

        usage.Add(promptTokens, completionTokens);
    }

    private async Task EnsureFeatureEnabledAsync(CancellationToken cancellationToken)
    {
        var flag = await adminStore.FindFlagAsync("assistant.enabled", false, cancellationToken);
        if (flag is { Enabled: false })
        {
            throw DomainException.Validation("assistant_disabled", "ProDoc Assistant is temporarily unavailable.");
        }
    }

    private async Task<AssistantConversation> RequireConversationAsync(Guid userId, Guid conversationId, bool tracking, CancellationToken cancellationToken)
    {
        var conversation = await store.FindConversationAsync(conversationId, userId, tracking, cancellationToken)
            ?? throw DomainException.NotFound("conversation_not_found", "That assistant conversation was not found.");
        if (conversation.UserId != userId)
        {
            throw DomainException.NotFound("conversation_not_found", "That assistant conversation was not found.");
        }

        return conversation;
    }

    private static string FallbackReply(
        string language,
        AssistantIntent intent,
        IReadOnlyList<AssistantDoctorRecommendation> recommendations,
        AssistantPendingActionDetails? pending)
    {
        if (language == "hi")
        {
            if (recommendations.Count > 0)
            {
                var lines = string.Join("\n", recommendations.Take(3).Select(item => $"• {item.DisplayName} — {item.Specialization} (₹{item.ConsultationFee})"));
                var suffix = pending is null ? "बुकिंग से पहले पुष्टि आवश्यक है।" : $"प्रस्तावित समय: {pending.Summary}. पुष्टि करने पर ही अपॉइंटमेंट बनेगा।";
                return $"{AssistantSafetyCopy.DisclaimerHi}\n\nसत्यापित सुझाव:\n{lines}\n\n{suffix}";
            }

            return intent == AssistantIntent.MedicalRecords
                ? $"{AssistantSafetyCopy.DisclaimerHi}\n\nरिकॉर्ड देखने के लिए AI सहमति दें। मैं केवल आपके अधिकृत तथ्य दिखाऊँगा और निदान/खुराक नहीं बताऊँगा।"
                : $"{AssistantSafetyCopy.DisclaimerHi}\n\nमैं डॉक्टर खोजने, बुकिंग समझाने और सामान्य जानकारी देने में मदद कर सकता हूँ।";
        }

        if (recommendations.Count > 0)
        {
            var lines = string.Join("\n", recommendations.Take(3).Select(item => $"• {item.DisplayName} — {item.Specialization} (₹{item.ConsultationFee})"));
            var suffix = pending is null
                ? "Booking still needs your explicit confirmation."
                : $"Proposed visit: {pending.Summary}. Nothing is booked until you confirm.";
            return $"{AssistantSafetyCopy.DisclaimerEn}\n\nVerified suggestions:\n{lines}\n\n{suffix}\nRatings are omitted until authentic reviews are available.";
        }

        return intent == AssistantIntent.MedicalRecords
            ? $"{AssistantSafetyCopy.DisclaimerEn}\n\nGrant AI records consent to review your authorized facts. I will not diagnose or recommend medicine changes."
            : $"{AssistantSafetyCopy.DisclaimerEn}\n\nI can help find verified doctors, explain booking steps, and share general health education.";
    }

    private static AssistantConsentDetails MapConsent(AssistantConsent? consent, string language) =>
        new(
            consent?.RecordsAccessAllowed ?? false,
            consent?.ConsentVersion ?? "v1",
            consent?.RecordsConsentAtUtc,
            language == "hi" ? AssistantSafetyCopy.DisclaimerHi : AssistantSafetyCopy.DisclaimerEn,
            language == "hi" ? AssistantSafetyCopy.EmergencyHi : AssistantSafetyCopy.EmergencyEn);

    private static AssistantConversationDetails MapConversation(AssistantConversation conversation)
    {
        var pending = ParsePending(conversation.PendingActionJson);
        return new AssistantConversationDetails(
            conversation.Id,
            conversation.Title,
            conversation.Language,
            conversation.PromptVersion,
            conversation.CreatedAtUtc,
            conversation.UpdatedAtUtc,
            pending,
            conversation.Messages
                .OrderBy(item => item.CreatedAtUtc)
                .Where(item => item.Role is AssistantMessageRole.User or AssistantMessageRole.Assistant)
                .Select(item => MapMessage(item, ParseSources(item.SourcesJson)))
                .ToArray(),
            []);
    }

    private static AssistantMessageDetails MapMessage(AssistantMessage message, IReadOnlyList<AssistantSourceRef> sources) =>
        new(message.Id, message.Role.ToString(), message.Content, sources, message.CreatedAtUtc, message.ToolName);

    private static IReadOnlyList<AssistantSourceRef> ParseSources(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var values = JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? [];
            return values.Select(value =>
            {
                var parts = value.Split(':', 2);
                return parts.Length == 2
                    ? new AssistantSourceRef(parts[0], parts[1], parts[1])
                    : new AssistantSourceRef("source", value, value);
            }).ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static AssistantPendingActionDetails? ParsePending(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<PendingActionPayload>(json, JsonOptions);
            return payload is null ? null : MapPending(payload);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static AssistantPendingActionDetails MapPending(PendingActionPayload payload) =>
        new(payload.ActionId, payload.Type, payload.Summary, payload.ScheduleId, payload.AppointmentId, payload.Date, payload.Start, payload.DoctorName);

    private AuditLog Audit(Guid userId, string action, RequestMetadata metadata) =>
        AuditLog.Record(userId, action, clock.UtcNow, metadata.IpAddress, metadata.UserAgent);

    private sealed record PendingActionPayload(
        Guid ActionId,
        string Type,
        Guid? ScheduleId,
        Guid? AppointmentId,
        string? Date,
        string? Start,
        string? DoctorName,
        string Summary);
}
