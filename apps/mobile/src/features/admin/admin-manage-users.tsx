import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import { Pressable, View } from 'react-native';

import { AdminFeedbackBanner } from '@/components/admin/admin-feedback';
import { AdminKpiCard } from '@/components/admin/admin-kpi-card';
import { AdminPanel } from '@/components/admin/admin-shell';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { StatusBadge } from '@/components/ui/status-badge';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { useCopy } from '@/i18n/copy';
import {
  deleteAdminUser,
  getAdminUser,
  listAdminClinics,
  reactivateUser,
  searchAdminUsers,
  suspendUser,
  type AdminUser,
} from '@/services/api/admin-api';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';
import { formatYearsExperience } from '@/utils/years-of-experience';

type RoleTab = 'all' | 'Doctor' | 'Receptionist' | 'ClinicStaff';

export function AdminManageUsersPanel() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const queryClient = useQueryClient();
  const [query, setQuery] = useState('');
  const [roleTab, setRoleTab] = useState<RoleTab>('all');
  const [status, setStatus] = useState<'all' | 'active' | 'suspended'>('all');
  const [clinicId, setClinicId] = useState('');
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);
  const [confirmSuspendId, setConfirmSuspendId] = useState<string | null>(null);
  const [suspendReason, setSuspendReason] = useState('');
  const [pendingUserId, setPendingUserId] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<{ message: string; tone: 'success' | 'error' } | null>(null);

  const clinics = useQuery({ queryKey: ['admin', 'clinics'], queryFn: () => listAdminClinics() });
  const users = useQuery({
    queryKey: ['admin', 'users', query, roleTab, status, clinicId],
    queryFn: () =>
      searchAdminUsers({
        q: query || undefined,
        role: roleTab === 'all' ? undefined : roleTab,
        suspended: status === 'all' ? undefined : status === 'suspended',
        clinicId: clinicId || undefined,
      }),
  });
  const details = useQuery({
    queryKey: ['admin', 'user', selectedId],
    queryFn: () => getAdminUser(selectedId!),
    enabled: Boolean(selectedId),
  });

  const counts = useMemo(() => {
    const rows = users.data ?? [];
    return {
      total: rows.length,
      doctors: rows.filter((item) => item.roles.includes('Doctor')).length,
      receptionists: rows.filter((item) => item.roles.includes('Receptionist')).length,
      staff: rows.filter((item) => item.roles.includes('ClinicStaff')).length,
      suspended: rows.filter((item) => item.suspendedAtUtc).length,
    };
  }, [users.data]);

  const invalidate = async (clearedUserId?: string) => {
    await queryClient.invalidateQueries({ queryKey: ['admin', 'users'] });
    if (clearedUserId) {
      queryClient.removeQueries({ queryKey: ['admin', 'user', clearedUserId] });
      return;
    }
    if (selectedId) {
      await queryClient.invalidateQueries({ queryKey: ['admin', 'user', selectedId] });
    }
  };

  const actionBusy = Boolean(pendingUserId);

  const suspend = useMutation({
    mutationFn: (userId: string) => suspendUser(userId, suspendReason || undefined),
    onMutate: (userId) => setPendingUserId(userId),
    onSuccess: async () => {
      setFeedback({ message: copy.adminUserDeactivated, tone: 'success' });
      setConfirmSuspendId(null);
      setSuspendReason('');
      await invalidate();
    },
    onError: (error) => setFeedback({ message: problemMessage(error, copy.adminSuspend), tone: 'error' }),
    onSettled: () => setPendingUserId(null),
  });

  const reactivate = useMutation({
    mutationFn: reactivateUser,
    onMutate: (userId) => setPendingUserId(userId),
    onSuccess: async () => {
      setFeedback({ message: copy.adminUserActivated, tone: 'success' });
      await invalidate();
    },
    onError: (error) => setFeedback({ message: problemMessage(error, copy.adminReactivate), tone: 'error' }),
    onSettled: () => setPendingUserId(null),
  });

  const remove = useMutation({
    mutationFn: deleteAdminUser,
    onMutate: (userId) => setPendingUserId(userId),
    onSuccess: async (_data, userId) => {
      setFeedback({ message: copy.adminUserDeleted, tone: 'success' });
      setConfirmDeleteId(null);
      if (selectedId === userId) {
        setSelectedId(null);
      }
      await invalidate(userId);
    },
    onError: (error) => setFeedback({ message: problemMessage(error, copy.adminDeleteUser), tone: 'error' }),
    onSettled: () => setPendingUserId(null),
  });

  const roleTabs: { id: RoleTab; label: string }[] = [
    { id: 'all', label: copy.adminFilterAll },
    { id: 'Doctor', label: copy.adminFilterDoctors },
    { id: 'Receptionist', label: copy.adminFilterReceptionists },
    { id: 'ClinicStaff', label: copy.adminFilterStaff },
  ];

  return (
    <View style={{ gap: spacing.lg }}>
      <AdminFeedbackBanner
        message={feedback?.message}
        tone={feedback?.tone ?? 'info'}
        onDismiss={() => setFeedback(null)}
      />

      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.md }}>
        <AdminKpiCard label={copy.adminUsers} value={String(counts.total)} />
        <AdminKpiCard label={copy.adminFilterDoctors} value={String(counts.doctors)} />
        <AdminKpiCard label={copy.adminFilterReceptionists} value={String(counts.receptionists)} />
        <AdminKpiCard label={copy.adminInactive} value={String(counts.suspended)} />
      </View>

      <AdminPanel title={copy.adminManageUsers} description={copy.adminManageUsersBody}>
        <TextField label={copy.search} value={query} onChangeText={setQuery} placeholder={copy.adminUserSearchPlaceholder} />

        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
          {roleTabs.map((tab) => (
            <Pressable
              key={tab.id}
              onPress={() => setRoleTab(tab.id)}
              style={{
                paddingHorizontal: 12,
                paddingVertical: 8,
                borderRadius: 999,
                backgroundColor: roleTab === tab.id ? theme.colors.primaryLight : '#EEF2F6',
                borderWidth: 1,
                borderColor: roleTab === tab.id ? `${theme.colors.primary}33` : 'transparent',
              }}>
              <AppText variant="label" style={{ color: roleTab === tab.id ? theme.colors.primary : theme.colors.textMuted }}>
                {tab.label}
              </AppText>
            </Pressable>
          ))}
        </View>

        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
          {(
            [
              { id: 'all' as const, label: copy.adminFilterAll },
              { id: 'active' as const, label: copy.adminActive },
              { id: 'suspended' as const, label: copy.adminInactive },
            ] as const
          ).map((item) => (
            <Pressable
              key={item.id}
              onPress={() => setStatus(item.id)}
              style={{
                paddingHorizontal: 12,
                paddingVertical: 8,
                borderRadius: 999,
                backgroundColor: status === item.id ? theme.colors.surface : '#EEF2F6',
                borderWidth: 1,
                borderColor: status === item.id ? theme.colors.primary : theme.colors.border,
              }}>
              <AppText variant="label" style={{ fontSize: 12, color: theme.colors.text }}>
                {item.label}
              </AppText>
            </Pressable>
          ))}
        </View>

        <AppText variant="label">{copy.adminFilterClinic}</AppText>
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
          <Pressable
            onPress={() => setClinicId('')}
            style={{
              paddingHorizontal: 12,
              paddingVertical: 8,
              borderRadius: 999,
              borderWidth: 1,
              borderColor: !clinicId ? theme.colors.primary : theme.colors.border,
            }}>
            <AppText variant="label" style={{ fontSize: 12 }}>
              {copy.adminFilterAll}
            </AppText>
          </Pressable>
          {(clinics.data ?? []).map((clinic) => (
            <Pressable
              key={clinic.id}
              onPress={() => setClinicId(clinic.id)}
              style={{
                paddingHorizontal: 12,
                paddingVertical: 8,
                borderRadius: 999,
                borderWidth: 1,
                borderColor: clinicId === clinic.id ? theme.colors.primary : theme.colors.border,
              }}>
              <AppText variant="label" style={{ fontSize: 12 }}>
                {clinic.name ?? clinic.reference}
              </AppText>
            </Pressable>
          ))}
        </View>

        {(users.data ?? []).map((user) => (
          <UserCard
            key={user.userId}
            user={user}
            selected={selectedId === user.userId}
            onSelect={() => setSelectedId(user.userId)}
            confirmingSuspend={confirmSuspendId === user.userId}
            suspendReason={suspendReason}
            onSuspendReason={setSuspendReason}
            onAskSuspend={() => {
              setConfirmDeleteId(null);
              setConfirmSuspendId(user.userId);
            }}
            onCancelSuspend={() => setConfirmSuspendId(null)}
            onConfirmSuspend={() => {
              if (actionBusy) {
                return;
              }
              suspend.mutate(user.userId);
            }}
            onReactivate={() => {
              if (actionBusy) {
                return;
              }
              reactivate.mutate(user.userId);
            }}
            onAskDelete={() => {
              setConfirmSuspendId(null);
              setConfirmDeleteId(user.userId);
            }}
            busy={pendingUserId === user.userId}
            actionsDisabled={actionBusy}
          />
        ))}
        {(users.data?.length ?? 0) === 0 ? <AppText variant="muted">{copy.adminEmptyUsers}</AppText> : null}
      </AdminPanel>

      {details.data ? (
        <AdminPanel title={copy.adminUserDetails} description={details.data.fullName ?? details.data.phoneNumber}>
          <AppText variant="muted">{details.data.roles.join(' · ') || '—'}</AppText>
          {details.data.email ? <AppText>{details.data.email}</AppText> : null}
          {details.data.primarySpecialization ? (
            <AppText>
              {copy.specialization}: {details.data.primarySpecialization}
            </AppText>
          ) : null}
          {details.data.doctorId != null && details.data.yearsOfExperience != null ? (
            <AppText>
              {copy.experience}: {formatYearsExperience(details.data.yearsOfExperience, copy.yearsExp)}
            </AppText>
          ) : null}
          {details.data.clinics.length > 0 ? (
            <View style={{ gap: spacing.xs }}>
              <AppText variant="label">{copy.adminAssignClinics}</AppText>
              {details.data.clinics.map((clinic) => (
                <AppText key={`${clinic.clinicId}-${clinic.role}`} variant="muted">
                  {clinic.clinicName} · {clinic.role}
                  {clinic.isActive ? '' : ` · ${copy.adminInactive}`}
                </AppText>
              ))}
            </View>
          ) : null}
          {details.data.assignedDoctors.length > 0 ? (
            <View style={{ gap: spacing.xs }}>
              <AppText variant="label">{copy.adminAssignDoctors}</AppText>
              {details.data.assignedDoctors.map((doctor) => (
                <AppText key={`${doctor.doctorId}-${doctor.clinicId}`} variant="muted">
                  {doctor.doctorName} · {doctor.clinicName}
                </AppText>
              ))}
            </View>
          ) : null}
        </AdminPanel>
      ) : null}

      <ConfirmDialog
        visible={Boolean(confirmDeleteId)}
        title={copy.adminConfirmDeleteUserTitle}
        body={copy.adminConfirmDeleteUser}
        confirmLabel={copy.adminDeleteUser}
        cancelLabel={copy.cancel}
        danger
        loading={remove.isPending}
        onConfirm={() => {
          if (!confirmDeleteId || actionBusy) {
            return;
          }
          remove.mutate(confirmDeleteId);
        }}
        onCancel={() => {
          if (remove.isPending) {
            return;
          }
          setConfirmDeleteId(null);
        }}
      />
    </View>
  );
}

function UserCard({
  user,
  selected,
  onSelect,
  confirmingSuspend,
  suspendReason,
  onSuspendReason,
  onAskSuspend,
  onCancelSuspend,
  onConfirmSuspend,
  onReactivate,
  onAskDelete,
  busy,
  actionsDisabled,
}: {
  user: AdminUser;
  selected: boolean;
  onSelect: () => void;
  confirmingSuspend: boolean;
  suspendReason: string;
  onSuspendReason: (value: string) => void;
  onAskSuspend: () => void;
  onCancelSuspend: () => void;
  onConfirmSuspend: () => void;
  onReactivate: () => void;
  onAskDelete: () => void;
  busy: boolean;
  actionsDisabled: boolean;
}) {
  const copy = useCopy();
  const theme = useProdocTheme();
  const inactive = Boolean(user.suspendedAtUtc);

  return (
    <Pressable
      onPress={onSelect}
      style={{
        borderWidth: 1,
        borderColor: selected ? theme.colors.primary : theme.colors.border,
        borderRadius: 14,
        padding: spacing.md,
        gap: spacing.sm,
        backgroundColor: selected ? theme.colors.primaryLight : theme.colors.surface,
      }}>
      <View style={{ flexDirection: 'row', justifyContent: 'space-between', gap: spacing.sm, alignItems: 'center' }}>
        <View style={{ flex: 1, gap: 2 }}>
          <AppText>
            {user.fullName || copy.welcome} · {user.phoneNumber}
          </AppText>
          <AppText variant="muted">{user.roles.join(', ') || '—'}</AppText>
        </View>
        <View style={{ alignItems: 'flex-end', gap: 4 }}>
          {inactive ? (
            <StatusBadge label={copy.adminInactive} tone="error" />
          ) : (
            <StatusBadge label={copy.adminActive} tone="success" />
          )}
          {user.doctorStatus ? (
            <StatusBadge
              label={user.doctorStatus}
              tone={user.doctorStatus === 'Approved' ? 'success' : 'warning'}
            />
          ) : null}
        </View>
      </View>

      {confirmingSuspend ? (
        <>
          <TextField label={copy.reason} value={suspendReason} onChangeText={onSuspendReason} />
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
            <Button
              label={copy.adminConfirmSuspend}
              variant="danger"
              loading={busy}
              disabled={actionsDisabled && !busy}
              onPress={onConfirmSuspend}
            />
            <Button label={copy.cancel} variant="secondary" disabled={busy} onPress={onCancelSuspend} />
          </View>
        </>
      ) : (
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
          {inactive ? (
            <Button
              label={copy.adminReactivate}
              loading={busy}
              disabled={actionsDisabled && !busy}
              onPress={onReactivate}
            />
          ) : (
            <Button
              label={copy.adminSuspend}
              variant="danger"
              disabled={actionsDisabled}
              onPress={onAskSuspend}
            />
          )}
          <Button
            label={copy.adminDeleteUser}
            variant="secondary"
            disabled={actionsDisabled}
            onPress={onAskDelete}
          />
        </View>
      )}
    </Pressable>
  );
}
