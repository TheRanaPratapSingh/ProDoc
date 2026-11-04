import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { useEffect, useMemo, useState } from 'react';
import { Pressable, ScrollView, View } from 'react-native';

import { Screen } from '@/components/layout/auth-scaffold';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { useCopy } from '@/i18n/copy';
import {
  confirmAssistantAction,
  deleteAssistantConversation,
  getAssistantConsent,
  sendAssistantMessage,
  setAssistantConsent,
  startAssistantConversation,
  type AssistantDoctorRecommendation,
  type AssistantMessage,
  type AssistantPendingAction,
} from '@/services/api/assistant-api';
import { useSessionStore } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

const suggestedKeys = ['assistantSuggestFind', 'assistantSuggestBook', 'assistantSuggestRecords', 'assistantSuggestExplain'] as const;

export function AssistantChatScreen() {
  const copy = useCopy();
  const router = useRouter();
  const theme = useProdocTheme();
  const queryClient = useQueryClient();
  const language = useSessionStore((state) => state.account?.preferredLanguage ?? 'en');
  const permissions = useSessionStore((state) => state.account?.permissions ?? []);
  const canUse = permissions.includes('assistant.use');
  const [conversationId, setConversationId] = useState<string | null>(null);
  const [messages, setMessages] = useState<AssistantMessage[]>([]);
  const [recommendations, setRecommendations] = useState<AssistantDoctorRecommendation[]>([]);
  const [pending, setPending] = useState<AssistantPendingAction | null>(null);
  const [input, setInput] = useState('');
  const [city, setCity] = useState('');
  const [error, setError] = useState<string>();

  const consent = useQuery({
    queryKey: ['assistant', 'consent', language],
    queryFn: () => getAssistantConsent(language),
    enabled: canUse,
  });

  const start = useMutation({
    mutationFn: () => startAssistantConversation(language),
    onSuccess: (conversation) => {
      setConversationId(conversation.id);
      setMessages(conversation.messages);
      setPending(conversation.pendingAction);
      setRecommendations(conversation.recommendations);
      setError(undefined);
    },
    onError: (caught) => setError(problemMessage(caught, copy.assistantTitle)),
  });

  useEffect(() => {
    if (canUse && !conversationId && !start.isPending && !start.data) {
      start.mutate();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- start once per mount when permitted
  }, [canUse, conversationId]);

  const send = useMutation({
    mutationFn: (content: string) =>
      sendAssistantMessage(conversationId!, {
        content,
        city: city || undefined,
      }),
    onSuccess: (turn, content) => {
      setMessages((prev) => [
        ...prev,
        {
          id: `local-${Date.now()}`,
          role: 'User',
          content,
          sources: [],
          createdAtUtc: new Date().toISOString(),
          toolName: null,
        },
        turn.assistantMessage,
      ]);
      setRecommendations(turn.recommendations);
      setPending(turn.pendingAction);
      setInput('');
      setError(undefined);
    },
    onError: (caught) => setError(problemMessage(caught, copy.assistantTitle)),
  });

  const confirm = useMutation({
    mutationFn: () => confirmAssistantAction(conversationId!, pending!.actionId),
    onSuccess: (result) => {
      setPending(null);
      setMessages((prev) => [
        ...prev,
        {
          id: `confirm-${result.appointmentId}`,
          role: 'Assistant',
          content: result.message,
          sources: result.appointmentId ? [{ kind: 'appointment', label: result.status, id: result.appointmentId }] : [],
          createdAtUtc: new Date().toISOString(),
          toolName: 'booking.confirm',
        },
      ]);
      setError(undefined);
    },
    onError: (caught) => setError(problemMessage(caught, copy.assistantConfirmBooking)),
  });

  const saveConsent = useMutation({
    mutationFn: (allow: boolean) => setAssistantConsent(allow, language),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['assistant', 'consent'] });
    },
    onError: (caught) => setError(problemMessage(caught, copy.assistantConsent)),
  });

  const remove = useMutation({
    mutationFn: () => deleteAssistantConversation(conversationId!),
    onSuccess: () => {
      setConversationId(null);
      setMessages([]);
      setPending(null);
      setRecommendations([]);
      start.mutate();
    },
  });

  const suggestions = useMemo(
    () =>
      suggestedKeys.map((key) => ({
        key,
        label: copy[key],
      })),
    [copy],
  );

  if (!canUse) {
    return (
      <Screen>
        <View style={{ padding: spacing.xl, gap: spacing.lg }}>
          <Button label={copy.back} variant="secondary" onPress={() => router.back()} />
          <AppText variant="title">{copy.assistantTitle}</AppText>
          <AppText variant="muted">{copy.assistantUnavailable}</AppText>
        </View>
      </Screen>
    );
  }

  return (
    <Screen>
      <ScrollView contentContainerStyle={{ padding: spacing.xl, gap: spacing.lg }} keyboardShouldPersistTaps="handled">
        <Button label={copy.back} variant="secondary" onPress={() => router.back()} />
        <AppText variant="title">{copy.assistantTitle}</AppText>
        <AppText variant="muted">{consent.data?.disclaimer ?? copy.assistantDisclaimer}</AppText>
        <Card>
          <AppText variant="label">{copy.assistantEmergency}</AppText>
          <AppText variant="muted">{consent.data?.emergencyGuidance ?? copy.assistantEmergencyBody}</AppText>
        </Card>
        <Card>
          <AppText variant="label">{copy.assistantConsent}</AppText>
          <AppText variant="muted">{copy.assistantConsentBody}</AppText>
          <Button
            label={consent.data?.recordsAccessAllowed ? copy.assistantRevokeConsent : copy.assistantGrantConsent}
            variant={consent.data?.recordsAccessAllowed ? 'danger' : 'secondary'}
            loading={saveConsent.isPending}
            onPress={() => saveConsent.mutate(!consent.data?.recordsAccessAllowed)}
          />
        </Card>
        <TextField label={copy.city} value={city} onChangeText={setCity} placeholder="Mumbai" />
        <View style={{ gap: spacing.sm }}>
          <AppText variant="label">{copy.assistantSuggestions}</AppText>
          {suggestions.map((item) => (
            <Pressable
              key={item.key}
              onPress={() => {
                setInput(item.label);
              }}
              style={{
                borderWidth: 1,
                borderColor: theme.colors.border,
                borderRadius: radius.md,
                padding: spacing.md,
                backgroundColor: theme.colors.surface,
              }}>
              <AppText>{item.label}</AppText>
            </Pressable>
          ))}
        </View>
        <Card>
          {messages.map((message) => (
            <View
              key={message.id}
              style={{
                alignSelf: message.role === 'User' ? 'flex-end' : 'flex-start',
                maxWidth: '92%',
                backgroundColor: message.role === 'User' ? theme.colors.brandTint : theme.colors.surface,
                borderRadius: radius.lg,
                borderWidth: 1,
                borderColor: theme.colors.border,
                padding: spacing.md,
                gap: spacing.xs,
              }}>
              <AppText>{message.content}</AppText>
              {message.sources?.length ? (
                <AppText variant="muted">
                  {copy.assistantSources}: {message.sources.map((source) => source.label).join(', ')}
                </AppText>
              ) : null}
            </View>
          ))}
          {messages.length === 0 ? <AppText variant="muted">{copy.loading}</AppText> : null}
        </Card>
        {recommendations.length > 0 ? (
          <Card>
            <AppText variant="label">{copy.assistantRecommendations}</AppText>
            {recommendations.map((item) => (
              <Card key={`${item.doctorId}-${item.scheduleId}`}>
                <AppText>
                  {item.displayName} · {item.specialization}
                </AppText>
                <AppText variant="muted">
                  {item.clinicName || copy.clinic}
                  {item.city ? ` · ${item.city}` : ''} · ₹{item.consultationFee}
                </AppText>
                <AppText variant="muted">{item.explanation}</AppText>
                {item.ratingNote ? <AppText variant="muted">{item.ratingNote}</AppText> : null}
                <Button
                  label={copy.assistantOpenDoctor}
                  variant="secondary"
                  onPress={() => router.push(`/practices/${item.doctorId}` as Href)}
                />
              </Card>
            ))}
          </Card>
        ) : null}
        {pending ? (
          <Card>
            <AppText variant="label">{copy.assistantPendingAction}</AppText>
            <AppText>{pending.summary}</AppText>
            <AppText variant="muted">{copy.assistantConfirmHint}</AppText>
            <Button label={copy.assistantConfirmBooking} loading={confirm.isPending} onPress={() => confirm.mutate()} />
            <Button label={copy.cancel} variant="secondary" onPress={() => setPending(null)} />
          </Card>
        ) : null}
        <TextField label={copy.assistantMessage} value={input} onChangeText={setInput} multiline />
        <Button
          label={copy.assistantSend}
          loading={send.isPending}
          disabled={!conversationId || input.trim().length < 1}
          onPress={() => send.mutate(input.trim())}
        />
        <Button
          label={copy.assistantDeleteChat}
          variant="danger"
          loading={remove.isPending}
          disabled={!conversationId}
          onPress={() => remove.mutate()}
        />
        {error ? <AppText variant="muted">{error}</AppText> : null}
      </ScrollView>
    </Screen>
  );
}
