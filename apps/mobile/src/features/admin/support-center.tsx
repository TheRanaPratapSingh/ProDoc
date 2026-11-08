import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { useState } from 'react';
import { Pressable, View } from 'react-native';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { useCopy } from '@/i18n/copy';
import {
  addSupportTicketNote,
  createSupportTicket,
  getMySupportTicket,
  listMySupportTickets,
} from '@/services/api/admin-api';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

const categories = [
  'AppointmentIssue',
  'PaymentIssue',
  'RefundRequest',
  'PatientComplaint',
  'ProviderComplaint',
  'AccountAccess',
  'TechnicalProblem',
  'PrescriptionAccess',
  'CancellationDispute',
  'BookingDispute',
  'Other',
] as const;

export function SupportCenter() {
  const copy = useCopy();
  const router = useRouter();
  const queryClient = useQueryClient();
  const [subject, setSubject] = useState('');
  const [description, setDescription] = useState('');
  const [category, setCategory] = useState<(typeof categories)[number]>('Other');
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [note, setNote] = useState('');

  const tickets = useQuery({ queryKey: ['support', 'mine'], queryFn: listMySupportTickets });
  const detail = useQuery({
    queryKey: ['support', 'ticket', selectedId],
    queryFn: () => getMySupportTicket(selectedId!),
    enabled: Boolean(selectedId),
  });

  const create = useMutation({
    mutationFn: () =>
      createSupportTicket({
        category,
        priority: 'Normal',
        subject,
        description,
      }),
    onSuccess: async (created) => {
      setSubject('');
      setDescription('');
      setSelectedId(created.id);
      await queryClient.invalidateQueries({ queryKey: ['support', 'mine'] });
    },
  });

  const reply = useMutation({
    mutationFn: () => addSupportTicketNote(selectedId!, note),
    onSuccess: async () => {
      setNote('');
      await queryClient.invalidateQueries({ queryKey: ['support', 'ticket', selectedId] });
      await queryClient.invalidateQueries({ queryKey: ['support', 'mine'] });
    },
  });

  const error = tickets.error ?? detail.error ?? create.error ?? reply.error;

  return (
    <AuthScaffold>
      <ScreenHeader title={copy.supportCenter} />
      <Button label={copy.helpCenterTitle} variant="secondary" onPress={() => router.push('/help' as Href)} />
      <Card>
        <AppText variant="label">{copy.supportCategory}</AppText>
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
          {categories.map((item) => (
            <Button
              key={item}
              label={item}
              variant={category === item ? 'primary' : 'secondary'}
              onPress={() => setCategory(item)}
            />
          ))}
        </View>
        <TextField label={copy.supportSubject} value={subject} onChangeText={setSubject} />
        <TextField label={copy.supportDescription} value={description} onChangeText={setDescription} multiline />
        <Button
          label={copy.supportSubmit}
          loading={create.isPending}
          onPress={() => create.mutate()}
          disabled={subject.trim().length < 4 || description.trim().length < 8}
        />
      </Card>
      <Card>
        <AppText variant="label">{copy.supportMyTickets}</AppText>
        {(tickets.data ?? []).map((item) => (
          <Pressable key={item.id} onPress={() => setSelectedId(item.id)}>
            <AppText>
              {item.number} · {item.subject}
            </AppText>
            <AppText variant="muted">
              {item.status} · {item.category}
            </AppText>
          </Pressable>
        ))}
        {tickets.data?.length === 0 ? <AppText variant="muted">{copy.adminEmptyTickets}</AppText> : null}
      </Card>
      {detail.data ? (
        <Card>
          <AppText variant="title">{detail.data.subject}</AppText>
          <AppText variant="muted">{detail.data.description}</AppText>
          <AppText variant="muted">
            {detail.data.number} · {detail.data.status}
          </AppText>
          {detail.data.notes.map((item) => (
            <AppText key={item.id} variant="muted">
              {item.body}
            </AppText>
          ))}
          <TextField label={copy.supportReply} value={note} onChangeText={setNote} />
          <Button label={copy.supportSendReply} loading={reply.isPending} onPress={() => reply.mutate()} />
        </Card>
      ) : null}
      {error ? <AppText variant="muted">{problemMessage(error, copy.supportCenter)}</AppText> : null}
    </AuthScaffold>
  );
}
