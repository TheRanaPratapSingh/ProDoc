import { useQuery } from '@tanstack/react-query';
import { useRouter, type Href } from 'expo-router';
import { memo, useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { Pressable, ScrollView, TextInput, View } from 'react-native';
import Animated, {
  Easing,
  cancelAnimation,
  useAnimatedStyle,
  useSharedValue,
  withRepeat,
  withSequence,
  withTiming,
} from 'react-native-reanimated';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { BottomSheet } from '@/components/ui/bottom-sheet';
import { Button } from '@/components/ui/button';
import {
  IconAppointments,
  IconBuilding,
  IconCalendarPlus,
  IconHelp,
  IconProfile,
  IconSearch,
  IconStethoscope,
  IconVaidyaAssistant,
} from '@/components/ui/prodoc-icons';
import { AppText } from '@/components/ui/text';
import {
  buildSpecialtyCards,
  type SpecialtyCardModel,
} from '@/features/account/specialty-images';
import { useActiveLanguage, useCopy } from '@/i18n/copy';
import { getSpecializations } from '@/services/api/provider-api';
import { useHasPermission } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';

type AssistantStep = 'menu' | 'specialty' | 'preference';

type MenuActionId = 'book' | 'doctors' | 'clinics' | 'visits' | 'profile' | 'help' | 'chat';

function VaidyaActionCard({
  title,
  body,
  icon,
  onPress,
}: {
  title: string;
  body: string;
  icon: ReactNode;
  onPress: () => void;
}) {
  const theme = useProdocTheme();
  return (
    <Pressable
      accessibilityRole="button"
      onPress={onPress}
      style={{
        flexDirection: 'row',
        gap: spacing.md,
        alignItems: 'center',
        padding: spacing.md,
        borderRadius: radius.lg,
        borderWidth: 1,
        borderColor: theme.colors.border,
        backgroundColor: theme.colors.surface,
      }}>
      <View
        style={{
          width: 44,
          height: 44,
          borderRadius: 22,
          alignItems: 'center',
          justifyContent: 'center',
          backgroundColor: theme.colors.primaryLight,
        }}>
        {icon}
      </View>
      <View style={{ flex: 1, gap: 2 }}>
        <AppText variant="label">{title}</AppText>
        <AppText variant="muted" style={{ fontSize: 13, lineHeight: 18 }}>
          {body}
        </AppText>
      </View>
    </Pressable>
  );
}

function SpecialtyChip({
  item,
  selected,
  onPress,
}: {
  item: SpecialtyCardModel;
  selected: boolean;
  onPress: () => void;
}) {
  const theme = useProdocTheme();
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ selected }}
      onPress={onPress}
      style={{
        paddingHorizontal: spacing.md,
        paddingVertical: spacing.sm,
        borderRadius: radius.pill,
        borderWidth: 1.5,
        borderColor: selected ? theme.colors.primary : theme.colors.border,
        backgroundColor: selected ? theme.colors.primaryLight : theme.colors.surface,
      }}>
      <AppText
        variant="label"
        style={{ color: selected ? theme.colors.primary : theme.colors.text, fontSize: 13 }}>
        {item.label}
      </AppText>
    </Pressable>
  );
}

/** Floating entry point for Vaidya Assistant on the patient home screen. */
export const VaidyaAssistantFab = memo(function VaidyaAssistantFab({ onPress }: { onPress: () => void }) {
  const theme = useProdocTheme();
  const copy = useCopy();
  const insets = useSafeAreaInsets();
  const reduce = theme.reduceMotion;
  const pulse = useSharedValue(1);

  useEffect(() => {
    if (reduce) {
      pulse.value = 1;
      return;
    }
    pulse.value = withRepeat(
      withSequence(
        withTiming(1.06, { duration: 1100, easing: Easing.inOut(Easing.quad) }),
        withTiming(1, { duration: 1100, easing: Easing.inOut(Easing.quad) }),
      ),
      -1,
      false,
    );
    return () => {
      cancelAnimation(pulse);
    };
  }, [pulse, reduce]);

  const animatedStyle = useAnimatedStyle(() => ({
    transform: [{ scale: reduce ? 1 : pulse.value }],
  }));

  // FAB lives in the home content area (above AppShell tab bar).
  const bottomOffset = Math.max(spacing.lg, insets.bottom > 0 ? spacing.md : spacing.lg);

  return (
    <Animated.View
      pointerEvents="box-none"
      style={[
        {
          position: 'absolute',
          right: spacing.lg,
          bottom: bottomOffset,
          zIndex: 40,
        },
        animatedStyle,
      ]}>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel={copy.vaidyaFabLabel}
        onPress={onPress}
        style={{
          width: 56,
          height: 56,
          borderRadius: 28,
          alignItems: 'center',
          justifyContent: 'center',
          backgroundColor: theme.colors.primary,
          borderWidth: 2,
          borderColor: theme.colors.surface,
          shadowColor: '#14243D',
          shadowOpacity: 0.18,
          shadowRadius: 10,
          shadowOffset: { width: 0, height: 4 },
          elevation: 6,
        }}>
        <IconVaidyaAssistant size={28} color={theme.colors.onPrimary} />
      </Pressable>
    </Animated.View>
  );
});

/** Guided Vaidya Assistant panel — routes into existing ProDoc screens. */
export function VaidyaAssistantPanel({
  visible,
  onClose,
}: {
  visible: boolean;
  onClose: () => void;
}) {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const language = useActiveLanguage();
  const canBook = useHasPermission('appointments.book.self');
  const canUseChat = useHasPermission('assistant.use');
  const [step, setStep] = useState<AssistantStep>('menu');
  const [specialtyQuery, setSpecialtyQuery] = useState('');
  const [selectedSpecialty, setSelectedSpecialty] = useState<SpecialtyCardModel | null>(null);

  const specializations = useQuery({
    queryKey: ['specializations'],
    queryFn: getSpecializations,
    staleTime: 5 * 60_000,
    enabled: visible,
  });

  const specialtyCards = useMemo(
    () => buildSpecialtyCards(specializations.data ?? [], language),
    [language, specializations.data],
  );

  const filteredSpecialties = useMemo(() => {
    const q = specialtyQuery.trim().toLowerCase();
    if (!q) return specialtyCards;
    return specialtyCards.filter(
      (item) =>
        item.label.toLowerCase().includes(q) ||
        item.name.toLowerCase().includes(q) ||
        item.code.toLowerCase().includes(q),
    );
  }, [specialtyCards, specialtyQuery]);

  const resetFlow = useCallback(() => {
    setStep('menu');
    setSpecialtyQuery('');
    setSelectedSpecialty(null);
  }, []);

  const handleClose = useCallback(() => {
    resetFlow();
    onClose();
  }, [onClose, resetFlow]);

  const navigateAndClose = useCallback(
    (href: Href) => {
      handleClose();
      // Defer navigation slightly so the sheet can dismiss without fighting the router.
      requestAnimationFrame(() => {
        router.push(href);
      });
    },
    [handleClose, router],
  );

  const onMenuAction = useCallback(
    (id: MenuActionId) => {
      switch (id) {
        case 'book':
          setStep('specialty');
          return;
        case 'doctors':
          navigateAndClose('/doctors' as Href);
          return;
        case 'clinics':
          navigateAndClose('/clinics' as Href);
          return;
        case 'visits':
          navigateAndClose('/visits' as Href);
          return;
        case 'profile':
          navigateAndClose('/home?tab=profile' as Href);
          return;
        case 'help':
          navigateAndClose('/help' as Href);
          return;
        case 'chat':
          navigateAndClose('/assistant' as Href);
          return;
      }
    },
    [navigateAndClose],
  );

  const onSpecialtyChosen = useCallback((item: SpecialtyCardModel) => {
    setSelectedSpecialty(item);
    setStep('preference');
  }, []);

  const continueWithPreference = useCallback(
    (mode: 'doctor' | 'clinic') => {
      if (!selectedSpecialty) return;
      if (mode === 'clinic') {
        navigateAndClose('/clinics' as Href);
        return;
      }
      navigateAndClose(
        `/doctors?specializationId=${encodeURIComponent(selectedSpecialty.id)}` as Href,
      );
    },
    [navigateAndClose, selectedSpecialty],
  );

  const primary = theme.colors.primary;

  return (
    <BottomSheet visible={visible} onClose={handleClose} maxHeightRatio={0.86} title={copy.assistantTitle}>
      <ScrollView
        keyboardShouldPersistTaps="handled"
        showsVerticalScrollIndicator={false}
        contentContainerStyle={{ gap: spacing.md, paddingBottom: spacing.sm }}>
        {step === 'menu' ? (
          <>
            <View style={{ flexDirection: 'row', gap: spacing.md, alignItems: 'center' }}>
              <View
                style={{
                  width: 48,
                  height: 48,
                  borderRadius: 24,
                  alignItems: 'center',
                  justifyContent: 'center',
                  backgroundColor: theme.colors.primary,
                }}>
                <IconVaidyaAssistant size={26} color={theme.colors.onPrimary} />
              </View>
              <View style={{ flex: 1, gap: 2 }}>
                <AppText variant="headline">{copy.vaidyaWelcomeTitle}</AppText>
                <AppText variant="muted">{copy.vaidyaWelcomeBody}</AppText>
              </View>
            </View>

            {canBook ? (
              <VaidyaActionCard
                title={copy.vaidyaMenuBook}
                body={copy.vaidyaMenuBookBody}
                icon={<IconCalendarPlus size={22} color={primary} />}
                onPress={() => onMenuAction('book')}
              />
            ) : null}
            <VaidyaActionCard
              title={copy.vaidyaMenuDoctors}
              body={copy.vaidyaMenuDoctorsBody}
              icon={<IconStethoscope size={20} color={primary} />}
              onPress={() => onMenuAction('doctors')}
            />
            <VaidyaActionCard
              title={copy.vaidyaMenuClinics}
              body={copy.vaidyaMenuClinicsBody}
              icon={<IconBuilding size={20} color={primary} />}
              onPress={() => onMenuAction('clinics')}
            />
            {canBook ? (
              <VaidyaActionCard
                title={copy.vaidyaMenuVisits}
                body={copy.vaidyaMenuVisitsBody}
                icon={<IconAppointments size={20} color={primary} />}
                onPress={() => onMenuAction('visits')}
              />
            ) : null}
            <VaidyaActionCard
              title={copy.vaidyaMenuProfile}
              body={copy.vaidyaMenuProfileBody}
              icon={<IconProfile size={20} color={primary} />}
              onPress={() => onMenuAction('profile')}
            />
            <VaidyaActionCard
              title={copy.vaidyaMenuHelp}
              body={copy.vaidyaMenuHelpBody}
              icon={<IconHelp size={20} color={primary} />}
              onPress={() => onMenuAction('help')}
            />
            {canUseChat ? (
              <VaidyaActionCard
                title={copy.vaidyaOpenChat}
                body={copy.assistantDisclaimer}
                icon={<IconSearch size={20} color={primary} />}
                onPress={() => onMenuAction('chat')}
              />
            ) : null}
          </>
        ) : null}

        {step === 'specialty' ? (
          <>
            <AppText variant="headline">{copy.vaidyaSpecialtyPrompt}</AppText>
            <TextInput
              value={specialtyQuery}
              onChangeText={setSpecialtyQuery}
              placeholder={copy.vaidyaSpecialtySearch}
              placeholderTextColor={theme.colors.textMuted}
              accessibilityLabel={copy.vaidyaSpecialtySearch}
              style={{
                borderWidth: 1.5,
                borderColor: theme.colors.border,
                borderRadius: radius.md,
                paddingHorizontal: spacing.md,
                paddingVertical: spacing.sm,
                color: theme.colors.text,
                backgroundColor: theme.colors.surface,
                fontSize: 15,
              }}
            />
            {specializations.isLoading ? <AppText variant="muted">{copy.loading}</AppText> : null}
            {specializations.isError ? (
              <AppText variant="muted" style={{ color: theme.colors.error }}>
                {copy.somethingWentWrong}
              </AppText>
            ) : null}
            {filteredSpecialties.length === 0 && !specializations.isLoading ? (
              <AppText variant="muted">{copy.vaidyaSpecialtyEmpty}</AppText>
            ) : (
              <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
                {filteredSpecialties.map((item) => (
                  <SpecialtyChip
                    key={item.id}
                    item={item}
                    selected={selectedSpecialty?.id === item.id}
                    onPress={() => onSpecialtyChosen(item)}
                  />
                ))}
              </View>
            )}
            <View style={{ flexDirection: 'row', gap: spacing.sm }}>
              <Button label={copy.vaidyaBack} variant="secondary" onPress={() => setStep('menu')} style={{ flex: 1 }} />
              <Button label={copy.vaidyaRestart} variant="ghost" onPress={resetFlow} style={{ flex: 1 }} />
            </View>
          </>
        ) : null}

        {step === 'preference' && selectedSpecialty ? (
          <>
            <AppText variant="muted">
              {copy.vaidyaSelectedSpecialty}: {selectedSpecialty.label}
            </AppText>
            <AppText variant="headline">{copy.vaidyaPreferencePrompt}</AppText>
            <VaidyaActionCard
              title={copy.vaidyaPreferenceDoctor}
              body={copy.vaidyaMenuDoctorsBody}
              icon={<IconStethoscope size={20} color={primary} />}
              onPress={() => continueWithPreference('doctor')}
            />
            <VaidyaActionCard
              title={copy.vaidyaPreferenceClinic}
              body={copy.vaidyaMenuClinicsBody}
              icon={<IconBuilding size={20} color={primary} />}
              onPress={() => continueWithPreference('clinic')}
            />
            <View style={{ flexDirection: 'row', gap: spacing.sm }}>
              <Button
                label={copy.vaidyaBack}
                variant="secondary"
                onPress={() => setStep('specialty')}
                style={{ flex: 1 }}
              />
              <Button label={copy.vaidyaRestart} variant="ghost" onPress={resetFlow} style={{ flex: 1 }} />
            </View>
          </>
        ) : null}

        <Button label={copy.vaidyaClose} variant="secondary" onPress={handleClose} />
      </ScrollView>
    </BottomSheet>
  );
}

/** Home-screen host: FAB + panel without remounting home content. */
export function VaidyaAssistantHost() {
  const [open, setOpen] = useState(false);
  return (
    <>
      <VaidyaAssistantFab onPress={() => setOpen(true)} />
      <VaidyaAssistantPanel visible={open} onClose={() => setOpen(false)} />
    </>
  );
}
