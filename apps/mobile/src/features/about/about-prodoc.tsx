import { useQuery } from '@tanstack/react-query';
import Constants from 'expo-constants';
import { useRouter, type Href } from 'expo-router';
import { useMemo, type ReactNode } from 'react';
import { Linking, Pressable, useWindowDimensions, View } from 'react-native';

import { Screen } from '@/components/layout/auth-scaffold';
import { FormScreenScroll } from '@/components/layout/keyboard-aware-form';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { ProdocLogo } from '@/components/ui/prodoc-logo';
import {
  IconAppointments,
  IconBuilding,
  IconCalendarPlus,
  IconClinic,
  IconHealthRecords,
  IconHelp,
  IconHeart,
  IconPeople,
  IconSearch,
  IconSpark,
  IconStethoscope,
} from '@/components/ui/prodoc-icons';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { getSupportCatalog } from '@/services/api/support-api';
import { useSessionStore } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { breakpoint, spacing } from '@/theme/tokens';

function FeatureCard({
  title,
  body,
  icon,
}: {
  title: string;
  body: string;
  icon: ReactNode;
}) {
  const theme = useProdocTheme();
  return (
    <Card
      style={{
        flex: 1,
        minWidth: 148,
        gap: spacing.sm,
        padding: spacing.md,
      }}>
      <View
        style={{
          width: 40,
          height: 40,
          borderRadius: 12,
          backgroundColor: theme.colors.primaryLight,
          alignItems: 'center',
          justifyContent: 'center',
        }}>
        {icon}
      </View>
      <AppText variant="label" style={{ fontSize: 15 }}>
        {title}
      </AppText>
      <AppText variant="muted" style={{ fontSize: 13, lineHeight: 18 }}>
        {body}
      </AppText>
    </Card>
  );
}

function BenefitRow({ title, body, icon }: { title: string; body: string; icon: ReactNode }) {
  const theme = useProdocTheme();
  return (
    <View style={{ flexDirection: 'row', gap: spacing.md, alignItems: 'flex-start' }}>
      <View
        style={{
          width: 36,
          height: 36,
          borderRadius: 10,
          backgroundColor: theme.colors.primaryLight,
          alignItems: 'center',
          justifyContent: 'center',
        }}>
        {icon}
      </View>
      <View style={{ flex: 1, gap: 2 }}>
        <AppText variant="label">{title}</AppText>
        <AppText variant="muted" style={{ fontSize: 13, lineHeight: 18 }}>
          {body}
        </AppText>
      </View>
    </View>
  );
}

export function AboutProDoc() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const router = useRouter();
  const { width } = useWindowDimensions();
  const accessToken = useSessionStore((state) => state.accessToken);
  const wide = width >= breakpoint.tablet;
  const pad = width >= breakpoint.wide ? spacing.xxxl : spacing.xl;

  const catalog = useQuery({
    queryKey: ['support', 'catalog'],
    queryFn: getSupportCatalog,
    enabled: Boolean(accessToken),
  });

  const appVersion = Constants.expoConfig?.version ?? Constants.nativeAppVersion ?? null;
  const buildVersion =
    Constants.nativeBuildVersion ??
    (Constants.expoConfig?.ios as { buildNumber?: string } | undefined)?.buildNumber ??
    (Constants.expoConfig?.android as { versionCode?: number } | undefined)?.versionCode?.toString() ??
    null;

  const supportEmail = catalog.data?.supportEmail?.trim() || null;
  const supportPhone = catalog.data?.supportPhone?.trim() || null;

  const features = useMemo(
    () => [
      {
        title: copy.aboutFeatureFindDoctors,
        body: copy.aboutFeatureFindDoctorsBody,
        icon: <IconSearch size={20} color={theme.colors.primary} />,
      },
      {
        title: copy.aboutFeatureClinics,
        body: copy.aboutFeatureClinicsBody,
        icon: <IconClinic size={20} color={theme.colors.primary} />,
      },
      {
        title: copy.aboutFeatureBook,
        body: copy.aboutFeatureBookBody,
        icon: <IconCalendarPlus size={20} color={theme.colors.primary} />,
      },
      {
        title: copy.aboutFeatureManage,
        body: copy.aboutFeatureManageBody,
        icon: <IconAppointments size={20} color={theme.colors.primary} />,
      },
      {
        title: copy.aboutFeatureFamily,
        body: copy.aboutFeatureFamilyBody,
        icon: <IconPeople size={20} color={theme.colors.primary} />,
      },
      {
        title: copy.aboutFeatureRecords,
        body: copy.aboutFeatureRecordsBody,
        icon: <IconHealthRecords size={20} color={theme.colors.primary} />,
      },
      {
        title: copy.aboutFeatureVaidya,
        body: copy.aboutFeatureVaidyaBody,
        icon: <IconSpark size={20} color={theme.colors.primary} />,
      },
      {
        title: copy.aboutFeatureSupport,
        body: copy.aboutFeatureSupportBody,
        icon: <IconHelp size={20} color={theme.colors.primary} />,
      },
    ],
    [copy, theme.colors.primary],
  );

  return (
    <Screen edges={['top', 'left', 'right', 'bottom']}>
      <FormScreenScroll
        contentContainerStyle={{
          gap: spacing.lg,
          paddingHorizontal: pad,
          paddingBottom: spacing.xxl,
          maxWidth: wide ? 720 : undefined,
          width: '100%',
          alignSelf: 'center',
        }}>
        <ScreenHeader title={copy.profileMenuAbout} showBack />

        <Card
          style={{
            gap: spacing.md,
            padding: spacing.lg,
            overflow: 'hidden',
            borderColor: theme.colors.primaryLight,
          }}>
          <View
            style={{
              position: 'absolute',
              right: -24,
              top: -24,
              width: 120,
              height: 120,
              borderRadius: 60,
              backgroundColor: theme.colors.primaryLight,
              opacity: 0.55,
            }}
          />
          <ProdocLogo size={48} />
          <AppText variant="muted" style={{ fontSize: 15, letterSpacing: 0.2 }}>
            {copy.tagline}
          </AppText>
          <AppText style={{ fontSize: 15, lineHeight: 22, color: theme.colors.text }}>
            {copy.aboutHeroBody}
          </AppText>
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.sm, marginTop: 4 }}>
            <View
              style={{
                width: 32,
                height: 32,
                borderRadius: 10,
                backgroundColor: theme.colors.primary,
                alignItems: 'center',
                justifyContent: 'center',
              }}>
              <IconStethoscope size={18} color="#FFFFFF" />
            </View>
            <AppText variant="muted" style={{ flex: 1, fontSize: 13 }}>
              {copy.aboutHeroBadge}
            </AppText>
          </View>
        </Card>

        <Card style={{ gap: spacing.sm, padding: spacing.lg }}>
          <AppText variant="label" style={{ fontSize: 16 }}>
            {copy.aboutWhatTitle}
          </AppText>
          <AppText variant="muted" style={{ fontSize: 14, lineHeight: 21 }}>
            {copy.aboutWhatBody}
          </AppText>
        </Card>

        <View style={{ gap: spacing.sm }}>
          <AppText variant="label" style={{ fontSize: 16, paddingHorizontal: 2 }}>
            {copy.aboutFeaturesTitle}
          </AppText>
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
            {features.map((item) => (
              <View
                key={item.title}
                style={{ width: wide ? '48%' : '100%', flexGrow: 1, maxWidth: wide ? '48%' : '100%' }}>
                <FeatureCard title={item.title} body={item.body} icon={item.icon} />
              </View>
            ))}
          </View>
        </View>

        <Card style={{ gap: spacing.md, padding: spacing.lg }}>
          <AppText variant="label" style={{ fontSize: 16 }}>
            {copy.aboutWhyTitle}
          </AppText>
          <BenefitRow
            title={copy.aboutWhyDiscovery}
            body={copy.aboutWhyDiscoveryBody}
            icon={<IconSearch size={18} color={theme.colors.primary} />}
          />
          <BenefitRow
            title={copy.aboutWhyBooking}
            body={copy.aboutWhyBookingBody}
            icon={<IconCalendarPlus size={18} color={theme.colors.primary} />}
          />
          <BenefitRow
            title={copy.aboutWhyVisibility}
            body={copy.aboutWhyVisibilityBody}
            icon={<IconBuilding size={18} color={theme.colors.primary} />}
          />
          <BenefitRow
            title={copy.aboutWhyConnected}
            body={copy.aboutWhyConnectedBody}
            icon={<IconHeart size={18} color={theme.colors.primary} />}
          />
        </Card>

        <Card style={{ gap: spacing.sm, padding: spacing.lg }}>
          <AppText variant="label" style={{ fontSize: 16 }}>
            {copy.aboutVisionTitle}
          </AppText>
          <AppText variant="muted" style={{ fontSize: 14, lineHeight: 21 }}>
            {copy.aboutVisionBody}
          </AppText>
        </Card>

        <Card style={{ gap: spacing.md, padding: spacing.lg }}>
          <AppText variant="label" style={{ fontSize: 16 }}>
            {copy.aboutHelpTitle}
          </AppText>
          <AppText variant="muted" style={{ fontSize: 14, lineHeight: 21 }}>
            {copy.aboutHelpBody}
          </AppText>
          <Button label={copy.helpCenterTitle} onPress={() => router.push('/help' as Href)} />
          <Button label={copy.helpFaqs} variant="secondary" onPress={() => router.push('/help' as Href)} />
          {supportEmail ? (
            <Pressable onPress={() => void Linking.openURL(`mailto:${supportEmail}`)}>
              <AppText variant="muted">
                {copy.helpSupportEmail}: {supportEmail}
              </AppText>
            </Pressable>
          ) : null}
          {supportPhone ? (
            <Pressable
              onPress={() => {
                const digits = supportPhone.replace(/[^\d+]/g, '');
                if (digits) {
                  void Linking.openURL(`tel:${digits}`);
                }
              }}>
              <AppText variant="muted">
                {copy.helpSupportPhone}: {supportPhone}
              </AppText>
            </Pressable>
          ) : null}
        </Card>

        <Card style={{ gap: spacing.sm, padding: spacing.lg }}>
          <AppText variant="label" style={{ fontSize: 16 }}>
            {copy.aboutLegalTitle}
          </AppText>
          {appVersion ? (
            <AppText variant="muted">
              {copy.aboutAppVersion}: {appVersion}
            </AppText>
          ) : null}
          {buildVersion ? (
            <AppText variant="muted">
              {copy.aboutBuildVersion}: {buildVersion}
            </AppText>
          ) : null}
          <AppText variant="muted" style={{ fontSize: 12, lineHeight: 18 }}>
            {copy.aboutLegalNote}
          </AppText>
        </Card>
      </FormScreenScroll>
    </Screen>
  );
}
