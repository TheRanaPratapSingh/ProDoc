import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import { ActivityIndicator, Pressable, View } from 'react-native';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { EmptyState } from '@/components/ui/empty-state';
import { IconHelp } from '@/components/ui/prodoc-icons';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { useActiveLanguage, useCopy } from '@/i18n/copy';
import {
  closeSupportTicket,
  getHelpArticle,
  getSupportCatalog,
  listHelpCategories,
  reopenSupportTicket,
  searchHelpArticles,
  submitHelpArticleFeedback,
  type SupportIssueType,
  type SupportTopic,
} from '@/services/api/support-api';
import {
  addSupportTicketNote,
  createSupportTicket,
  getMySupportTicket,
  listMySupportTickets,
} from '@/services/api/admin-api';
import { listAppointments } from '@/services/api/provider-api';
import { useSessionStore } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

type HubTab = 'home' | 'faqs' | 'ticket' | 'mine';

export function HelpSupportHub() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const language = useActiveLanguage();
  const queryClient = useQueryClient();
  const account = useSessionStore((state) => state.account);
  const [tab, setTab] = useState<HubTab>('home');
  const [selectedTopic, setSelectedTopic] = useState<SupportTopic | null>(null);
  const [selectedIssue, setSelectedIssue] = useState<SupportIssueType | null>(null);
  const [faqQuery, setFaqQuery] = useState('');
  const [faqCategory, setFaqCategory] = useState<string | undefined>();
  const [selectedSlug, setSelectedSlug] = useState<string | null>(null);
  const [subject, setSubject] = useState('');
  const [description, setDescription] = useState('');
  const [appointmentId, setAppointmentId] = useState<string | undefined>();
  const [selectedTicketId, setSelectedTicketId] = useState<string | null>(null);
  const [reply, setReply] = useState('');
  const [submittedNumber, setSubmittedNumber] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  const catalog = useQuery({ queryKey: ['support', 'catalog'], queryFn: getSupportCatalog });
  const categories = useQuery({ queryKey: ['help', 'categories'], queryFn: listHelpCategories });
  const articles = useQuery({
    queryKey: ['help', 'articles', faqQuery, faqCategory],
    queryFn: () => searchHelpArticles(faqQuery.trim() || undefined, faqCategory),
    enabled: tab === 'faqs' || tab === 'home',
  });
  const article = useQuery({
    queryKey: ['help', 'article', selectedSlug],
    queryFn: () => getHelpArticle(selectedSlug!),
    enabled: Boolean(selectedSlug),
  });
  const tickets = useQuery({
    queryKey: ['support', 'mine'],
    queryFn: listMySupportTickets,
    enabled: tab === 'mine' || tab === 'ticket',
  });
  const detail = useQuery({
    queryKey: ['support', 'ticket', selectedTicketId],
    queryFn: () => getMySupportTicket(selectedTicketId!),
    enabled: Boolean(selectedTicketId),
  });
  const appointments = useQuery({
    queryKey: ['appointments', 'mine', 'support'],
    queryFn: listAppointments,
    enabled: tab === 'ticket',
  });

  const primaryRole = account?.roles?.[0] ?? 'Patient';
  const hi = language === 'hi';

  const create = useMutation({
    mutationFn: () =>
      createSupportTicket({
        category: selectedIssue?.ticketCategory ?? 'Other',
        priority: selectedIssue?.defaultPriority ?? 'Normal',
        subject: subject.trim(),
        description: description.trim(),
        appointmentId,
        issueType: selectedIssue?.code,
        createdByRole: primaryRole,
      }),
    onSuccess: async (created) => {
      setSubmittedNumber(created.number);
      setSubject('');
      setDescription('');
      setAppointmentId(undefined);
      setSelectedTicketId(created.id);
      await queryClient.invalidateQueries({ queryKey: ['support', 'mine'] });
      setTab('mine');
    },
  });

  const sendReply = useMutation({
    mutationFn: () => addSupportTicketNote(selectedTicketId!, reply),
    onSuccess: async () => {
      setReply('');
      await queryClient.invalidateQueries({ queryKey: ['support', 'ticket', selectedTicketId] });
      await queryClient.invalidateQueries({ queryKey: ['support', 'mine'] });
    },
  });

  const closeTicket = useMutation({
    mutationFn: () => closeSupportTicket(selectedTicketId!),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['support'] });
    },
  });

  const reopenTicket = useMutation({
    mutationFn: () => reopenSupportTicket(selectedTicketId!, reply || undefined),
    onSuccess: async () => {
      setReply('');
      await queryClient.invalidateQueries({ queryKey: ['support'] });
    },
  });

  const feedback = useMutation({
    mutationFn: (wasHelpful: boolean) => submitHelpArticleFeedback(selectedSlug!, wasHelpful),
  });

  const tabs: { id: HubTab; label: string }[] = [
    { id: 'home', label: copy.helpSupportHome },
    { id: 'faqs', label: copy.helpFaqs },
    { id: 'ticket', label: copy.helpRaiseTicket },
    { id: 'mine', label: copy.helpMyRequests },
  ];

  const validateTicket = () => {
    const errors: Record<string, string> = {};
    if (!selectedIssue) errors.issue = copy.helpIssueRequired;
    if (subject.trim().length < 4) errors.subject = copy.helpSubjectRequired;
    if (description.trim().length < 8) errors.description = copy.helpDescriptionRequired;
    setFieldErrors(errors);
    return Object.keys(errors).length === 0;
  };

  const topicCards = useMemo(() => catalog.data?.topics ?? [], [catalog.data]);

  return (
    <AuthScaffold>
      <ScreenHeader title={copy.profileMenuHelp} />
      <AppText variant="headline">{copy.helpHowCanWeHelp}</AppText>
      <AppText variant="muted">{copy.helpHowCanWeHelpBody}</AppText>

      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
        {tabs.map((item) => {
          const active = tab === item.id;
          return (
            <Pressable
              key={item.id}
              onPress={() => setTab(item.id)}
              style={{
                paddingHorizontal: 12,
                paddingVertical: 8,
                borderRadius: 999,
                borderWidth: 1,
                borderColor: active ? theme.colors.primary : theme.colors.border,
                backgroundColor: active ? theme.colors.primaryLight : theme.colors.surface,
              }}>
              <AppText variant="caption" style={{ color: active ? theme.colors.primary : theme.colors.text }}>
                {item.label}
              </AppText>
            </Pressable>
          );
        })}
      </View>

      {tab === 'home' ? (
        <View style={{ gap: spacing.md }}>
          {catalog.isLoading ? <ActivityIndicator color={theme.colors.primary} /> : null}
          {topicCards.map((topic) => (
            <Pressable
              key={topic.code}
              onPress={() => {
                setSelectedTopic(topic);
                setSelectedIssue(topic.issueTypes[0] ?? null);
                setFaqCategory(topic.helpCategoryCode);
                setTab('ticket');
              }}>
              <Card style={{ gap: spacing.sm }}>
                <View style={{ flexDirection: 'row', gap: spacing.md, alignItems: 'center' }}>
                  <IconHelp size={22} color={theme.colors.primary} />
                  <View style={{ flex: 1, gap: 4 }}>
                    <AppText variant="label">{hi ? topic.titleHi : topic.titleEn}</AppText>
                    <AppText variant="muted">{hi ? topic.bodyHi : topic.bodyEn}</AppText>
                  </View>
                </View>
              </Card>
            </Pressable>
          ))}

          <Card style={{ gap: spacing.sm }}>
            <AppText variant="label">{copy.helpHumanSupport}</AppText>
            {catalog.data?.liveAgentsAvailable ? (
              <AppText variant="muted">{copy.helpHumanSupportOnline}</AppText>
            ) : (
              <AppText variant="muted">{copy.helpHumanSupportOffline}</AppText>
            )}
            <AppText variant="caption">
              {copy.helpSupportEmail}: {catalog.data?.supportEmail ?? 'support@prodoc.app'}
            </AppText>
            <AppText variant="caption">
              {copy.helpSupportPhone}: {catalog.data?.supportPhone ?? '+91-1800-000-000'}
            </AppText>
            <Button label={copy.helpRaiseTicket} onPress={() => setTab('ticket')} />
            <Button label={copy.helpFaqs} variant="secondary" onPress={() => setTab('faqs')} />
          </Card>
        </View>
      ) : null}

      {tab === 'faqs' ? (
        <View style={{ gap: spacing.md }}>
          <Card style={{ gap: spacing.sm }}>
            <TextField
              label={copy.helpSearch}
              value={faqQuery}
              onChangeText={setFaqQuery}
              placeholder={copy.helpSearchPlaceholder}
            />
            <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
              <Button
                label={copy.helpAllCategories}
                variant={faqCategory ? 'secondary' : 'primary'}
                onPress={() => setFaqCategory(undefined)}
              />
              {(categories.data ?? []).map((item) => (
                <Button
                  key={item.id}
                  label={hi ? item.nameHi : item.nameEn}
                  variant={faqCategory === item.code ? 'primary' : 'secondary'}
                  onPress={() => setFaqCategory(item.code)}
                />
              ))}
            </View>
          </Card>
          <Card style={{ gap: spacing.sm }}>
            {(articles.data ?? []).map((item) => (
              <Pressable key={item.id} onPress={() => setSelectedSlug(item.slug)} style={{ gap: 4 }}>
                <AppText variant="label">{hi ? item.titleHi : item.titleEn}</AppText>
                <AppText variant="caption">{item.categoryCode}</AppText>
              </Pressable>
            ))}
            {articles.data?.length === 0 ? <EmptyState title={copy.helpEmpty} compact /> : null}
          </Card>
          {article.data ? (
            <Card style={{ gap: spacing.sm }}>
              <AppText variant="title">{hi ? article.data.titleHi : article.data.titleEn}</AppText>
              <AppText variant="muted">{hi ? article.data.bodyHi : article.data.bodyEn}</AppText>
              <AppText variant="label">{copy.helpWasThisHelpful}</AppText>
              <View style={{ flexDirection: 'row', gap: spacing.sm }}>
                <Button label={copy.helpYes} variant="secondary" onPress={() => feedback.mutate(true)} />
                <Button label={copy.helpNo} variant="secondary" onPress={() => feedback.mutate(false)} />
              </View>
              {feedback.isSuccess ? <AppText variant="caption">{copy.helpFeedbackThanks}</AppText> : null}
              <Button
                label={copy.helpStillNeedHelp}
                onPress={() => {
                  setTab('ticket');
                }}
              />
            </Card>
          ) : null}
        </View>
      ) : null}

      {tab === 'ticket' ? (
        <View style={{ gap: spacing.md }}>
          {submittedNumber ? (
            <Card style={{ gap: spacing.sm }}>
              <AppText variant="label">{copy.helpTicketSubmitted}</AppText>
              <AppText>
                {copy.helpTicketId}: {submittedNumber}
              </AppText>
            </Card>
          ) : null}
          <Card style={{ gap: spacing.sm }}>
            <AppText variant="label">
              {copy.helpCategory} <AppText style={{ color: theme.colors.error }}>*</AppText>
            </AppText>
            <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
              {topicCards.map((topic) => (
                <Button
                  key={topic.code}
                  label={hi ? topic.titleEn : topic.titleEn}
                  variant={selectedTopic?.code === topic.code ? 'primary' : 'secondary'}
                  onPress={() => {
                    setSelectedTopic(topic);
                    setSelectedIssue(topic.issueTypes[0] ?? null);
                  }}
                />
              ))}
            </View>

            {selectedTopic ? (
              <>
                <AppText variant="label">
                  {copy.helpIssueType} <AppText style={{ color: theme.colors.error }}>*</AppText>
                </AppText>
                {fieldErrors.issue ? <AppText variant="caption" style={{ color: theme.colors.error }}>{fieldErrors.issue}</AppText> : null}
                <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
                  {selectedTopic.issueTypes.map((issue) => (
                    <Button
                      key={issue.code}
                      label={hi ? issue.labelHi : issue.labelEn}
                      variant={selectedIssue?.code === issue.code ? 'primary' : 'secondary'}
                      onPress={() => setSelectedIssue(issue)}
                    />
                  ))}
                </View>
              </>
            ) : null}

            <TextField
              label={`${copy.supportSubject} *`}
              value={subject}
              onChangeText={setSubject}
              error={fieldErrors.subject}
            />
            <TextField
              label={`${copy.supportDescription} *`}
              value={description}
              onChangeText={setDescription}
              multiline
              error={fieldErrors.description}
            />

            <AppText variant="label">{copy.helpRelatedAppointment}</AppText>
            <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
              <Button
                label={copy.helpNoRelated}
                variant={!appointmentId ? 'primary' : 'secondary'}
                onPress={() => setAppointmentId(undefined)}
              />
              {(appointments.data ?? []).slice(0, 8).map((item) => (
                <Button
                  key={item.id}
                  label={`${item.reference ?? item.id.slice(0, 8)} · ${item.status}`}
                  variant={appointmentId === item.id ? 'primary' : 'secondary'}
                  onPress={() => setAppointmentId(item.id)}
                />
              ))}
            </View>

            <AppText variant="caption">
              {account?.fullName || '—'} · {account?.phoneNumber || '—'} · {primaryRole}
            </AppText>

            <Button
              label={copy.supportSubmit}
              loading={create.isPending}
              onPress={() => {
                if (!validateTicket()) return;
                create.mutate();
              }}
            />
            {create.error ? (
              <AppText variant="muted">{problemMessage(create.error, copy.supportCenter)}</AppText>
            ) : null}
          </Card>
        </View>
      ) : null}

      {tab === 'mine' ? (
        <View style={{ gap: spacing.md }}>
          {(tickets.data ?? []).length === 0 ? <EmptyState title={copy.helpNoTickets} compact /> : null}
          {(tickets.data ?? []).map((item) => (
            <Pressable key={item.id} onPress={() => setSelectedTicketId(item.id)}>
              <Card style={{ gap: 4, opacity: selectedTicketId === item.id ? 1 : 0.92 }}>
                <AppText variant="label">{item.number}</AppText>
                <AppText>{item.subject}</AppText>
                <AppText variant="caption">
                  {item.category} · {item.status} · {new Date(item.updatedAtUtc).toLocaleString()}
                </AppText>
              </Card>
            </Pressable>
          ))}

          {detail.data ? (
            <Card style={{ gap: spacing.sm }}>
              <AppText variant="title">{detail.data.subject}</AppText>
              <AppText variant="muted">{detail.data.description}</AppText>
              <AppText variant="caption">
                {detail.data.number} · {detail.data.status}
                {detail.data.assignedToUserId ? ` · ${copy.helpAssigned}` : ''}
              </AppText>
              {(detail.data.notes ?? []).map((note) => (
                <Card key={note.id} style={{ gap: 4 }}>
                  <AppText variant="muted">{note.body}</AppText>
                  <AppText variant="caption">{new Date(note.createdAtUtc).toLocaleString()}</AppText>
                </Card>
              ))}
              <TextField label={copy.supportReply} value={reply} onChangeText={setReply} multiline />
              <Button label={copy.supportSendReply} loading={sendReply.isPending} onPress={() => sendReply.mutate()} />
              {detail.data.status === 'Resolved' || detail.data.status === 'WaitingOnUser' ? (
                <Button label={copy.helpCloseTicket} variant="secondary" onPress={() => closeTicket.mutate()} />
              ) : null}
              {detail.data.status === 'Resolved' || detail.data.status === 'Closed' ? (
                <Button label={copy.helpReopenTicket} variant="secondary" onPress={() => reopenTicket.mutate()} />
              ) : null}
            </Card>
          ) : null}
        </View>
      ) : null}

      {catalog.error || categories.error || tickets.error ? (
        <AppText variant="muted">
          {problemMessage(catalog.error ?? categories.error ?? tickets.error, copy.profileMenuHelp)}
        </AppText>
      ) : null}
    </AuthScaffold>
  );
}
