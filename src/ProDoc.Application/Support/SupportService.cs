using Microsoft.Extensions.Logging;
using ProDoc.Application.Abstractions;
using ProDoc.Application.Admin;
using ProDoc.Application.Automation;
using ProDoc.Application.Billing;
using ProDoc.Application.Providers;
using ProDoc.Domain.Admin;
using ProDoc.Domain.Automation;
using ProDoc.Domain.Common;
using ProDoc.Domain.Governance;
using ProDoc.Domain.Identity;
using ProDoc.Domain.Queue;
using ProDoc.Domain.Support;

namespace ProDoc.Application.Support;

public sealed class SupportService(
    ISupportStore store,
    ISupportFileStore files,
    FinanceService finance,
    IAutomationEventBus automation,
    IClock clock,
    ILogger<SupportService> logger)
{
    private static readonly TimeSpan NotificationCooldown = TimeSpan.FromMinutes(2);

    public async Task SeedHelpCenterAsync(CancellationToken cancellationToken)
    {
        var categories = await store.ListCategoriesAsync(false, cancellationToken);
        if (categories.Count == 0)
        {
            var defs = new (string Code, string En, string Hi, int Order)[]
            {
                ("booking", "Booking", "बुकिंग", 1),
                ("payments", "Payments & refunds", "भुगतान और रिफंड", 2),
                ("cancellation", "Cancellation policies", "रद्दीकरण नीति", 3),
                ("onboarding", "Doctor & clinic onboarding", "डॉक्टर और क्लिनिक ऑनबोर्डिंग", 4),
                ("records", "Prescriptions & records", "प्रिस्क्रिप्शन और रिकॉर्ड", 5),
                ("account", "Account & security", "खाता और सुरक्षा", 6),
                ("faq", "FAQs", "अक्सर पूछे जाने वाले प्रश्न", 7)
            };
            foreach (var def in defs)
            {
                store.AddCategory(HelpArticleCategory.Create(def.Code, def.En, def.Hi, def.Order));
            }

            await store.SaveChangesAsync(cancellationToken);
            categories = await store.ListCategoriesAsync(false, cancellationToken);
        }

        var articles = await store.SearchArticlesAsync(null, null, false, 5, cancellationToken);
        if (articles.Count > 0)
        {
            return;
        }

        var byCode = categories.ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);
        var seedUser = Guid.Empty;
        void Article(string code, string slug, string titleEn, string titleHi, string bodyEn, string bodyHi, string tags)
        {
            store.AddArticle(HelpArticle.Create(
                byCode[code].Id,
                slug,
                titleEn,
                titleHi,
                bodyEn,
                bodyHi,
                tags,
                seedUser,
                clock.UtcNow,
                publish: true));
        }

        Article(
            "booking",
            "how-to-book",
            "How to book a visit",
            "विज़िट कैसे बुक करें",
            "Search for a verified doctor, choose an available slot, hold the time, then confirm. Offline holds are never treated as confirmed bookings.",
            "सत्यापित डॉक्टर खोजें, उपलब्ध स्लॉट चुनें, होल्ड करें, फिर कन्फर्म करें। ऑफ़लाइन होल्ड को पुष्टि बुकिंग नहीं माना जाता।",
            "booking,slots");
        Article(
            "payments",
            "payments-and-refunds",
            "Payments and refunds",
            "भुगतान और रिफंड",
            "Pay online or at the clinic per clinic policy. Refunds follow authorized refund workflows and never edit ledgers directly.",
            "क्लिनिक नीति के अनुसार ऑनलाइन या क्लिनिक में भुगतान करें। रिफंड अधिकृत वर्कफ़्लो से होते हैं और लेजर सीधे नहीं बदलते।",
            "payment,refund");
        Article(
            "cancellation",
            "cancellation-policy",
            "Cancellation policy basics",
            "रद्दीकरण नीति की मूल बातें",
            "Patients can cancel before the clinic cutoff. Late cancellations may follow clinic rules shown on the visit.",
            "मरीज़ क्लिनिक कटऑफ़ से पहले रद्द कर सकते हैं। देर से रद्दीकरण विज़िट पर दिखी नियमों का पालन करता है।",
            "cancel");
        Article(
            "onboarding",
            "provider-onboarding",
            "Doctor and clinic onboarding",
            "डॉक्टर और क्लिनिक ऑनबोर्डिंग",
            "Submit profile details and documents for verification. Publish schedules only after verification completes.",
            "सत्यापन के लिए प्रोफ़ाइल और दस्तावेज़ जमा करें। सत्यापन पूरा होने के बाद ही शेड्यूल प्रकाशित करें।",
            "doctor,clinic");
        Article(
            "records",
            "prescriptions-and-records",
            "Prescriptions and medical records",
            "प्रिस्क्रिप्शन और मेडिकल रिकॉर्ड",
            "Finalized prescriptions appear in Health Records. Share records only with explicit consent.",
            "अंतिम प्रिस्क्रिप्शन हेल्थ रिकॉर्ड्स में दिखते हैं। रिकॉर्ड केवल स्पष्ट सहमति से साझा करें।",
            "prescription,records");
        Article(
            "account",
            "account-security",
            "Account and security",
            "खाता और सुरक्षा",
            "Sign in with OTP. Request account deletion from your profile. Never share OTP codes.",
            "OTP से साइन इन करें। प्रोफ़ाइल से खाता हटाने का अनुरोध करें। OTP कभी साझा न करें।",
            "security,otp");
        Article(
            "faq",
            "common-questions",
            "Common questions",
            "सामान्य प्रश्न",
            "Need help with a visit, payment, or account? Open a support ticket from Support. Agents never ask for passwords.",
            "विज़िट, भुगतान या खाते में मदद चाहिए? सहायता से टिकट खोलें। एजेंट पासवर्ड नहीं माँगते।",
            "faq,support");
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HelpCategoryDetails>> ListCategoriesAsync(bool activeOnly, CancellationToken cancellationToken) =>
        (await store.ListCategoriesAsync(activeOnly, cancellationToken))
            .Select(item => new HelpCategoryDetails(item.Id, item.Code, item.NameEn, item.NameHi, item.SortOrder, item.IsActive))
            .ToArray();

    public async Task<IReadOnlyList<HelpArticleSummary>> SearchHelpAsync(string? query, string? categoryCode, bool publishedOnly, CancellationToken cancellationToken)
    {
        Guid? categoryId = null;
        if (!string.IsNullOrWhiteSpace(categoryCode))
        {
            var category = await store.FindCategoryByCodeAsync(categoryCode.Trim(), cancellationToken)
                ?? throw DomainException.NotFound("help_category_not_found", "That help category was not found.");
            categoryId = category.Id;
        }

        var categories = (await store.ListCategoriesAsync(false, cancellationToken)).ToDictionary(item => item.Id);
        var rows = await store.SearchArticlesAsync(query, categoryId, publishedOnly, 100, cancellationToken);
        return rows.Select(item => new HelpArticleSummary(
            item.Id,
            item.CategoryId,
            categories.TryGetValue(item.CategoryId, out var cat) ? cat.Code : string.Empty,
            item.Slug,
            item.TitleEn,
            item.TitleHi,
            item.Tags,
            item.IsPublished,
            item.UpdatedAtUtc)).ToArray();
    }

    public async Task<HelpArticleDetails> GetHelpArticleAsync(string slugOrId, bool allowUnpublished, CancellationToken cancellationToken)
    {
        HelpArticle? article = Guid.TryParse(slugOrId, out var id)
            ? await store.FindArticleAsync(id, false, cancellationToken)
            : await store.FindArticleBySlugAsync(slugOrId.Trim().ToLowerInvariant(), cancellationToken);
        if (article is null)
        {
            throw DomainException.NotFound("help_article_not_found", "That help article was not found.");
        }

        if (!allowUnpublished && !article.IsPublished)
        {
            throw DomainException.NotFound("help_article_not_found", "That help article was not found.");
        }

        var category = await store.FindCategoryAsync(article.CategoryId, false, cancellationToken);
        return MapArticle(article, category?.Code ?? string.Empty);
    }

    public async Task<HelpArticleDetails> UpsertHelpArticleAsync(
        Guid adminUserId,
        Guid? articleId,
        Guid categoryId,
        string slug,
        string titleEn,
        string titleHi,
        string bodyEn,
        string bodyHi,
        string? tags,
        bool isPublished,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        _ = await store.FindCategoryAsync(categoryId, false, cancellationToken)
            ?? throw DomainException.NotFound("help_category_not_found", "That help category was not found.");

        HelpArticle article;
        if (articleId is Guid id)
        {
            article = await store.FindArticleAsync(id, true, cancellationToken)
                ?? throw DomainException.NotFound("help_article_not_found", "That help article was not found.");
            article.Update(categoryId, titleEn, titleHi, bodyEn, bodyHi, tags, isPublished, adminUserId, clock.UtcNow);
        }
        else
        {
            article = HelpArticle.Create(categoryId, slug, titleEn, titleHi, bodyEn, bodyHi, tags, adminUserId, clock.UtcNow, isPublished);
            store.AddArticle(article);
        }

        await store.AddAuditAsync(Audit(adminUserId, AuditActions.HelpArticleUpdated, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        var category = await store.FindCategoryAsync(article.CategoryId, false, cancellationToken);
        return MapArticle(article, category?.Code ?? string.Empty);
    }

    public async Task<SupportTicketDetails> CreateTicketAsync(
        Guid userId,
        string category,
        string priority,
        string subject,
        string description,
        Guid? appointmentId,
        Guid? paymentId,
        Guid? clinicId,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<SupportTicketCategory>(category, true, out var parsedCategory)
            || !Enum.TryParse<SupportTicketPriority>(priority, true, out var parsedPriority))
        {
            throw DomainException.Validation("ticket_category_invalid", "Choose a valid category and priority.");
        }

        var number = await store.NextTicketNumberAsync(cancellationToken);
        var ticket = SupportTicket.Create(
            number,
            userId,
            parsedCategory,
            parsedPriority,
            subject,
            description,
            clock.UtcNow,
            appointmentId,
            paymentId,
            clinicId);
        store.AddTicket(ticket);
        await store.AddStatusHistoryAsync(
            TicketStatusHistory.Record(ticket.Id, userId, "None", ticket.Status.ToString(), "Ticket created", clock.UtcNow),
            cancellationToken);
        await store.AddAuditAsync(Audit(userId, AuditActions.SupportTicketCreated, metadata), cancellationToken);
        await NotifyAsync(userId, "support.ticket.created", ticket.Id, "Support ticket created", $"Ticket {ticket.Number} was created.", cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return await MapTicketAsync(ticket, includeInternal: false, cancellationToken);
    }

    public async Task<IReadOnlyList<SupportTicketSummary>> ListMyTicketsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await store.QueryTicketsAsync(userId, null, null, null, false, false, 100, cancellationToken);
        return rows.Select(MapSummary).ToArray();
    }

    public async Task<AgentTicketWorkspace> AgentWorkspaceAsync(
        Guid agentUserId,
        string? status,
        string? priority,
        string? filter,
        CancellationToken cancellationToken)
    {
        SupportTicketStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<SupportTicketStatus>(status, true, out var st))
        {
            parsedStatus = st;
        }

        SupportTicketPriority? minPriority = null;
        if (!string.IsNullOrWhiteSpace(priority) && Enum.TryParse<SupportTicketPriority>(priority, true, out var pr))
        {
            minPriority = pr;
        }

        var mode = (filter ?? "open").Trim().ToLowerInvariant();
        var open = await store.QueryTicketsAsync(null, null, SupportTicketStatus.Open, null, false, false, 200, cancellationToken);
        var pending = await store.QueryTicketsAsync(null, null, SupportTicketStatus.WaitingOnUser, null, false, false, 200, cancellationToken);
        var high = await store.QueryTicketsAsync(null, null, null, SupportTicketPriority.High, false, false, 200, cancellationToken);
        var mine = await store.QueryTicketsAsync(null, agentUserId, null, null, false, false, 200, cancellationToken);
        var overdue = await store.QueryTicketsAsync(null, null, null, null, true, false, 200, cancellationToken);

        IReadOnlyList<SupportTicket> focus = mode switch
        {
            "pending" => pending,
            "high" => high,
            "assigned" => mine,
            "overdue" => overdue,
            "all" => await store.QueryTicketsAsync(null, null, parsedStatus, minPriority, false, false, 200, cancellationToken),
            _ => open
        };

        return new AgentTicketWorkspace(
            open.Count,
            pending.Count,
            high.Count(item => item.Status is SupportTicketStatus.Open or SupportTicketStatus.InProgress or SupportTicketStatus.WaitingOnUser),
            mine.Count(item => item.Status is not (SupportTicketStatus.Resolved or SupportTicketStatus.Closed)),
            overdue.Count,
            focus.Select(MapSummary).ToArray());
    }

    public async Task<SupportTicketDetails> GetTicketAsync(Guid userId, Guid ticketId, bool isAgent, CancellationToken cancellationToken)
    {
        var ticket = await store.FindTicketAsync(ticketId, false, cancellationToken)
            ?? throw DomainException.NotFound("ticket_not_found", "That support ticket was not found.");
        if (!isAgent && ticket.CreatedByUserId != userId)
        {
            throw DomainException.NotFound("ticket_not_found", "That support ticket was not found.");
        }

        return await MapTicketAsync(ticket, includeInternal: isAgent, cancellationToken);
    }

    public async Task<SupportTicketDetails> UpdateTicketAsync(
        Guid agentUserId,
        Guid ticketId,
        string? status,
        string? priority,
        Guid? assignedToUserId,
        string? note,
        bool internalNote,
        string? resolutionDetails,
        bool escalate,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var ticket = await store.FindTicketAsync(ticketId, true, cancellationToken)
            ?? throw DomainException.NotFound("ticket_not_found", "That support ticket was not found.");
        var previous = ticket.Status.ToString();

        if (escalate)
        {
            ticket.Escalate(clock.UtcNow);
            await NotifyAsync(ticket.CreatedByUserId, "support.ticket.escalated", ticket.Id, "Support ticket escalated", $"Ticket {ticket.Number} was escalated.", cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<SupportTicketStatus>(status, true, out var parsed))
            {
                throw DomainException.Validation("ticket_status_invalid", "Choose a valid ticket status.");
            }

            ticket.UpdateStatus(parsed, clock.UtcNow, resolutionDetails);
            if (!string.Equals(previous, ticket.Status.ToString(), StringComparison.Ordinal))
            {
                await store.AddStatusHistoryAsync(
                    TicketStatusHistory.Record(ticket.Id, agentUserId, previous, ticket.Status.ToString(), resolutionDetails ?? note, clock.UtcNow),
                    cancellationToken);
                await NotifyAsync(
                    ticket.CreatedByUserId,
                    "support.ticket.status",
                    ticket.Id,
                    "Support ticket updated",
                    $"Ticket {ticket.Number} is now {ticket.Status}.",
                    cancellationToken);
            }
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            if (!Enum.TryParse<SupportTicketPriority>(priority, true, out var parsedPriority))
            {
                throw DomainException.Validation("ticket_priority_invalid", "Choose a valid ticket priority.");
            }

            ticket.SetPriority(parsedPriority, clock.UtcNow);
        }

        if (assignedToUserId.HasValue)
        {
            ticket.Assign(assignedToUserId.Value == Guid.Empty ? null : assignedToUserId, clock.UtcNow);
        }

        if (!string.IsNullOrWhiteSpace(note))
        {
            var created = ticket.AddNote(agentUserId, note, internalNote, clock.UtcNow);
            store.AddTicketNote(created);
            if (!internalNote)
            {
                await NotifyAsync(
                    ticket.CreatedByUserId,
                    "support.ticket.reply",
                    ticket.Id,
                    "New support reply",
                    $"There is a new reply on ticket {ticket.Number}.",
                    cancellationToken);
            }
        }

        await store.AddAuditAsync(Audit(agentUserId, AuditActions.SupportTicketUpdated, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        if (!string.Equals(previous, ticket.Status.ToString(), StringComparison.Ordinal))
        {
            try
            {
                await automation.PublishAsync(
                    AutomationTriggerTypes.SupportTicketStatusChanged,
                    new
                    {
                        ticketId = ticket.Id,
                        userId = ticket.CreatedByUserId,
                        status = ticket.Status.ToString(),
                        previous
                    },
                    $"support.status:{ticket.Id:N}:{ticket.Status}:{ticket.UpdatedAtUtc.UtcTicks}",
                    cancellationToken,
                    subjectId: ticket.Id,
                    userId: ticket.CreatedByUserId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to publish support automation event for ticket {TicketId}", ticket.Id);
            }
        }

        var reloaded = await store.FindTicketAsync(ticketId, false, cancellationToken) ?? ticket;
        return await MapTicketAsync(reloaded, includeInternal: true, cancellationToken);
    }

    public async Task<SupportTicketDetails> AddUserNoteAsync(Guid userId, Guid ticketId, string note, RequestMetadata metadata, CancellationToken cancellationToken)
    {
        var ticket = await store.FindTicketAsync(ticketId, true, cancellationToken)
            ?? throw DomainException.NotFound("ticket_not_found", "That support ticket was not found.");
        if (ticket.CreatedByUserId != userId)
        {
            throw DomainException.NotFound("ticket_not_found", "That support ticket was not found.");
        }

        var created = ticket.AddNote(userId, note, isInternal: false, clock.UtcNow);
        store.AddTicketNote(created);
        await store.AddAuditAsync(Audit(userId, AuditActions.SupportTicketUpdated, metadata), cancellationToken);
        if (ticket.AssignedToUserId is Guid agentId)
        {
            await NotifyAsync(agentId, "support.ticket.customer_reply", ticket.Id, "Customer replied", $"Ticket {ticket.Number} has a customer reply.", cancellationToken);
        }

        await store.SaveChangesAsync(cancellationToken);
        return await MapTicketAsync(ticket, includeInternal: false, cancellationToken);
    }

    public async Task<SupportAttachmentDetails> AddAttachmentAsync(
        Guid userId,
        Guid ticketId,
        string fileName,
        string contentType,
        Stream content,
        bool isAgent,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var ticket = await store.FindTicketAsync(ticketId, false, cancellationToken)
            ?? throw DomainException.NotFound("ticket_not_found", "That support ticket was not found.");
        if (!isAgent && ticket.CreatedByUserId != userId)
        {
            throw DomainException.NotFound("ticket_not_found", "That support ticket was not found.");
        }

        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        await using var read = new MemoryStream(bytes);
        var key = await files.SaveAsync(ticketId, fileName, read, cancellationToken);
        var attachment = SupportAttachment.Create(ticketId, userId, fileName, contentType, key, bytes.LongLength, clock.UtcNow);
        store.AddAttachment(attachment);
        await store.AddAuditAsync(Audit(userId, AuditActions.SupportAttachmentAdded, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return new SupportAttachmentDetails(attachment.Id, attachment.FileName, attachment.ContentType, attachment.SizeBytes, attachment.UploadedByUserId, attachment.CreatedAtUtc);
    }

    public async Task<(Stream Stream, string FileName, string ContentType)> DownloadAttachmentAsync(
        Guid userId,
        Guid attachmentId,
        bool isAgent,
        CancellationToken cancellationToken)
    {
        var attachment = await store.FindAttachmentAsync(attachmentId, cancellationToken)
            ?? throw DomainException.NotFound("support_attachment_not_found", "That attachment was not found.");
        var ticket = await store.FindTicketAsync(attachment.SupportTicketId, false, cancellationToken)
            ?? throw DomainException.NotFound("ticket_not_found", "That support ticket was not found.");
        if (!isAgent && ticket.CreatedByUserId != userId)
        {
            throw DomainException.NotFound("support_attachment_not_found", "That attachment was not found.");
        }

        var stream = await files.OpenReadAsync(attachment.StorageKey, cancellationToken);
        return (stream, attachment.FileName, attachment.ContentType);
    }

    public async Task<DisputeCaseDetails> OpenDisputeAsync(
        Guid agentUserId,
        Guid ticketId,
        string kind,
        string summary,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<DisputeCaseKind>(kind, true, out var parsedKind))
        {
            throw DomainException.Validation("dispute_kind_invalid", "Choose a valid dispute kind.");
        }

        var ticket = await store.FindTicketAsync(ticketId, true, cancellationToken)
            ?? throw DomainException.NotFound("ticket_not_found", "That support ticket was not found.");
        if (await store.FindDisputeByTicketAsync(ticketId, cancellationToken) is not null)
        {
            throw DomainException.Conflict("dispute_exists", "A dispute case already exists for this ticket.");
        }

        var dispute = DisputeCase.Open(
            await store.NextDisputeNumberAsync(cancellationToken),
            ticketId,
            agentUserId,
            parsedKind,
            summary,
            clock.UtcNow,
            ticket.PaymentId,
            ticket.AppointmentId);
        store.AddDispute(dispute);
        ticket.Escalate(clock.UtcNow);
        await store.AddAuditAsync(Audit(agentUserId, AuditActions.DisputeOpened, metadata), cancellationToken);
        await NotifyAsync(ticket.CreatedByUserId, "support.dispute.opened", dispute.Id, "Dispute opened", $"Dispute {dispute.Number} was opened for ticket {ticket.Number}.", cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapDispute(dispute, ticket.Number);
    }

    public async Task<IReadOnlyList<DisputeCaseSummary>> ListDisputesAsync(string? status, CancellationToken cancellationToken)
    {
        DisputeCaseStatus? parsed = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<DisputeCaseStatus>(status, true, out var value))
        {
            parsed = value;
        }

        var rows = await store.ListDisputesAsync(parsed, 200, cancellationToken);
        return rows.Select(MapDisputeSummary).ToArray();
    }

    public async Task<DisputeCaseDetails> GetDisputeAsync(Guid disputeId, CancellationToken cancellationToken)
    {
        var dispute = await store.FindDisputeAsync(disputeId, false, cancellationToken)
            ?? throw DomainException.NotFound("dispute_not_found", "That dispute case was not found.");
        var ticket = await store.FindTicketAsync(dispute.SupportTicketId, false, cancellationToken);
        return MapDispute(dispute, ticket?.Number ?? string.Empty);
    }

    public async Task<DisputeCaseDetails> UpdateDisputeAsync(
        Guid agentUserId,
        Guid disputeId,
        string? action,
        string? evidenceRequest,
        string? decision,
        string? decisionNotes,
        decimal? refundAmount,
        RequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var dispute = await store.FindDisputeAsync(disputeId, true, cancellationToken)
            ?? throw DomainException.NotFound("dispute_not_found", "That dispute case was not found.");
        var ticket = await store.FindTicketAsync(dispute.SupportTicketId, false, cancellationToken)
            ?? throw DomainException.NotFound("ticket_not_found", "That support ticket was not found.");

        var mode = (action ?? string.Empty).Trim().ToLowerInvariant();
        if (mode == "assign")
        {
            dispute.Assign(agentUserId, clock.UtcNow);
        }
        else if (mode == "request-evidence")
        {
            if (string.IsNullOrWhiteSpace(evidenceRequest))
            {
                throw DomainException.Validation("dispute_evidence_required", "Describe the evidence requested.");
            }

            dispute.RequestEvidence(evidenceRequest, clock.UtcNow);
            await NotifyAsync(ticket.CreatedByUserId, "support.dispute.evidence", dispute.Id, "Evidence requested", $"Additional details were requested for dispute {dispute.Number}.", cancellationToken);
        }
        else if (mode == "escalate")
        {
            dispute.Escalate(clock.UtcNow);
            await NotifyAsync(ticket.CreatedByUserId, "support.dispute.escalated", dispute.Id, "Dispute escalated", $"Dispute {dispute.Number} was escalated.", cancellationToken);
        }
        else if (mode == "resolve")
        {
            if (!Enum.TryParse<DisputeDecision>(decision, true, out var parsedDecision) || parsedDecision == DisputeDecision.None)
            {
                throw DomainException.Validation("dispute_decision_invalid", "Choose a valid dispute decision.");
            }

            Guid? refundId = null;
            if (parsedDecision is DisputeDecision.PartialRefund or DisputeDecision.FullRefund)
            {
                if (dispute.LinkedPaymentId is not Guid paymentId)
                {
                    throw DomainException.Validation("dispute_payment_required", "A linked payment is required for refund decisions.");
                }

                var refund = await finance.RequestSupportRefundAsync(
                    agentUserId,
                    paymentId,
                    parsedDecision == DisputeDecision.FullRefund ? null : refundAmount,
                    decisionNotes ?? $"Dispute {dispute.Number} refund",
                    metadata,
                    cancellationToken);
                refundId = refund.Id;
            }

            dispute.Resolve(parsedDecision, decisionNotes ?? parsedDecision.ToString(), refundId, clock.UtcNow);
            await NotifyAsync(ticket.CreatedByUserId, "support.dispute.resolved", dispute.Id, "Dispute resolved", $"Dispute {dispute.Number} was resolved.", cancellationToken);
        }
        else if (mode == "close")
        {
            dispute.Close(clock.UtcNow);
        }
        else
        {
            throw DomainException.Validation("dispute_action_invalid", "Choose assign, request-evidence, escalate, resolve, or close.");
        }

        await store.AddAuditAsync(Audit(agentUserId, AuditActions.DisputeUpdated, metadata), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return MapDispute(dispute, ticket.Number);
    }

    public Task<SupportAnalytics> AnalyticsAsync(CancellationToken cancellationToken) =>
        store.BuildAnalyticsAsync(clock.UtcNow.AddDays(-30), cancellationToken);

    private async Task NotifyAsync(Guid userId, string kind, Guid subjectId, string title, string body, CancellationToken cancellationToken)
    {
        var since = clock.UtcNow.Subtract(NotificationCooldown);
        if (await store.HasRecentNotificationAsync(userId, kind, subjectId, since, cancellationToken))
        {
            return;
        }

        await store.NotifyAsync(UserNotification.Create(userId, kind, subjectId, title, body, clock.UtcNow), cancellationToken);
    }

    private async Task<SupportTicketDetails> MapTicketAsync(SupportTicket ticket, bool includeInternal, CancellationToken cancellationToken)
    {
        var attachments = (await store.ListAttachmentsAsync(ticket.Id, cancellationToken))
            .Select(item => new SupportAttachmentDetails(item.Id, item.FileName, item.ContentType, item.SizeBytes, item.UploadedByUserId, item.CreatedAtUtc))
            .ToArray();
        var history = includeInternal
            ? (await store.ListStatusHistoryAsync(ticket.Id, cancellationToken))
                .Select(item => new TicketStatusHistoryDetails(item.Id, item.FromStatus, item.ToStatus, item.ActorUserId, item.Note, item.ChangedAtUtc))
                .ToArray()
            : Array.Empty<TicketStatusHistoryDetails>();

        return new SupportTicketDetails(
            ticket.Id,
            ticket.Number,
            ticket.Category.ToString(),
            ticket.Priority.ToString(),
            ticket.Status.ToString(),
            ticket.Subject,
            ticket.Description,
            ticket.CreatedByUserId,
            ticket.AssignedToUserId,
            ticket.AppointmentId,
            ticket.PaymentId,
            ticket.ClinicId,
            ticket.CreatedAtUtc,
            ticket.UpdatedAtUtc,
            ticket.ResolvedAtUtc,
            ticket.Notes
                .Where(note => includeInternal || !note.IsInternal)
                .OrderBy(note => note.CreatedAtUtc)
                .Select(note => new SupportTicketNoteDetails(note.Id, note.AuthorUserId, note.Body, note.IsInternal, note.CreatedAtUtc))
                .ToArray(),
            ticket.ClosedAtUtc,
            ticket.EscalatedAtUtc,
            ticket.DueAtUtc,
            includeInternal ? ticket.ResolutionDetails : null,
            attachments,
            history);
    }

    private SupportTicketSummary MapSummary(SupportTicket ticket) =>
        new(
            ticket.Id,
            ticket.Number,
            ticket.Category.ToString(),
            ticket.Priority.ToString(),
            ticket.Status.ToString(),
            ticket.Subject,
            ticket.CreatedByUserId,
            ticket.AssignedToUserId,
            ticket.CreatedAtUtc,
            ticket.UpdatedAtUtc,
            ticket.DueAtUtc,
            ticket.EscalatedAtUtc,
            ticket.DueAtUtc is DateTimeOffset due
                && due < clock.UtcNow
                && ticket.Status is SupportTicketStatus.Open or SupportTicketStatus.InProgress or SupportTicketStatus.WaitingOnUser);

    private static HelpArticleDetails MapArticle(HelpArticle article, string categoryCode) =>
        new(
            article.Id,
            article.CategoryId,
            categoryCode,
            article.Slug,
            article.TitleEn,
            article.TitleHi,
            article.BodyEn,
            article.BodyHi,
            article.Tags,
            article.IsPublished,
            article.UpdatedAtUtc);

    private static DisputeCaseSummary MapDisputeSummary(DisputeCase dispute) =>
        new(
            dispute.Id,
            dispute.Number,
            dispute.SupportTicketId,
            dispute.Kind.ToString(),
            dispute.Status.ToString(),
            dispute.Decision.ToString(),
            dispute.Summary,
            dispute.AssignedToUserId,
            dispute.LinkedPaymentId,
            dispute.LinkedAppointmentId,
            dispute.RefundId,
            dispute.CreatedAtUtc,
            dispute.UpdatedAtUtc,
            dispute.ResolvedAtUtc);

    private static DisputeCaseDetails MapDispute(DisputeCase dispute, string ticketNumber) =>
        new(
            dispute.Id,
            dispute.Number,
            dispute.SupportTicketId,
            ticketNumber,
            dispute.Kind.ToString(),
            dispute.Status.ToString(),
            dispute.Decision.ToString(),
            dispute.Summary,
            dispute.EvidenceRequest,
            dispute.DecisionNotes,
            dispute.OpenedByUserId,
            dispute.AssignedToUserId,
            dispute.LinkedPaymentId,
            dispute.LinkedAppointmentId,
            dispute.RefundId,
            dispute.CreatedAtUtc,
            dispute.UpdatedAtUtc,
            dispute.ResolvedAtUtc);

    private AuditLog Audit(Guid userId, string action, RequestMetadata metadata) =>
        AuditLog.Record(userId, action, clock.UtcNow, metadata.IpAddress, metadata.UserAgent);
}
