import { type Href } from 'expo-router';
import { type ReactNode, useMemo, useState } from 'react';
import {
  Modal,
  Pressable,
  ScrollView,
  TextInput,
  useWindowDimensions,
  View,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import {
  AdminIconMenu,
  AdminIconSearch,
} from '@/components/admin/admin-icons';
import { FormScreenScroll } from '@/components/layout/keyboard-aware-form';
import { NotificationBell } from '@/components/ui/notification-bell';
import { IconBack } from '@/components/ui/prodoc-icons';
import { ProdocLogo } from '@/components/ui/prodoc-logo';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { useSessionStore } from '@/stores/session-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { breakpoint, radius, spacing } from '@/theme/tokens';

export type AdminNavItem = {
  id: string;
  label: string;
  visible: boolean;
  icon: ReactNode;
  group?: string;
};

export function AdminShell({
  title,
  subtitle,
  activeId,
  items,
  onSelect,
  onBack,
  showBack = false,
  onLogout,
  children,
  searchValue,
  onSearchChange,
}: {
  title: string;
  subtitle?: string;
  activeId: string;
  items: AdminNavItem[];
  onSelect: (id: string) => void;
  onBack?: () => void;
  /** Only show the header/drawer back control when true. */
  showBack?: boolean;
  onLogout?: () => void;
  children: ReactNode;
  searchValue?: string;
  onSearchChange?: (value: string) => void;
}) {
  const theme = useProdocTheme();
  const copy = useCopy();
  const insets = useSafeAreaInsets();
  const { width } = useWindowDimensions();
  const desktop = width >= breakpoint.wide;
  const tablet = width >= breakpoint.tablet;
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [profileOpen, setProfileOpen] = useState(false);
  const [logoutConfirmOpen, setLogoutConfirmOpen] = useState(false);
  const account = useSessionStore((state) => state.account);
  const visibleItems = useMemo(() => items.filter((item) => item.visible), [items]);

  const select = (id: string) => {
    if (id !== activeId) {
      onSelect(id);
    }
    setDrawerOpen(false);
  };

  const handleBack = () => {
    onBack?.();
  };

  const requestLogout = () => {
    setProfileOpen(false);
    setDrawerOpen(false);
    setLogoutConfirmOpen(true);
  };

  const confirmLogout = () => {
    setLogoutConfirmOpen(false);
    onLogout?.();
  };

  const navList = (
    <ScrollView
      style={{ flex: 1 }}
      contentContainerStyle={{ paddingVertical: spacing.md, paddingHorizontal: spacing.sm, gap: 2 }}
      showsVerticalScrollIndicator={false}>
      {visibleItems.map((item) => {
        const active = item.id === activeId;
        return (
          <Pressable
            key={item.id}
            accessibilityRole="button"
            accessibilityState={{ selected: active }}
            onPress={() => select(item.id)}
            style={{
              flexDirection: 'row',
              alignItems: 'center',
              gap: spacing.sm,
              paddingVertical: 10,
              paddingHorizontal: 12,
              borderRadius: 12,
              backgroundColor: active ? theme.colors.primaryLight : 'transparent',
              borderWidth: active ? 1 : 0,
              borderColor: active ? `${theme.colors.primary}33` : 'transparent',
            }}>
            <View style={{ width: 22, alignItems: 'center' }}>{item.icon}</View>
            <AppText
              variant="label"
              style={{
                flex: 1,
                color: active ? theme.colors.primary : theme.colors.text,
                fontSize: 13,
                lineHeight: 18,
              }}
              numberOfLines={1}>
              {item.label}
            </AppText>
          </Pressable>
        );
      })}
    </ScrollView>
  );

  const sidebarFooter = (
    <View style={{ marginHorizontal: spacing.md, marginBottom: spacing.sm, gap: spacing.sm }}>
      {showBack && onBack ? (
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={copy.back}
          onPress={handleBack}
          style={{
            flexDirection: 'row',
            alignItems: 'center',
            gap: spacing.sm,
            paddingVertical: 10,
            paddingHorizontal: 12,
            borderRadius: 12,
          }}>
          <IconBack size={18} color={theme.colors.textMuted} />
          <AppText variant="label" style={{ color: theme.colors.textMuted }}>
            {copy.back}
          </AppText>
        </Pressable>
      ) : null}
      {onLogout ? (
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={copy.signOut}
          onPress={requestLogout}
          style={{
            paddingVertical: 10,
            paddingHorizontal: 12,
            borderRadius: 12,
            borderWidth: 1,
            borderColor: theme.colors.border,
            backgroundColor: theme.colors.background,
          }}>
          <AppText variant="label" style={{ color: theme.colors.error, textAlign: 'center' }}>
            {copy.signOut}
          </AppText>
        </Pressable>
      ) : null}
    </View>
  );

  const sidebar = (
    <View
      style={{
        width: desktop ? 248 : 220,
        backgroundColor: theme.colors.surface,
        borderRightWidth: 1,
        borderRightColor: theme.colors.border,
        paddingTop: Math.max(insets.top, spacing.md),
        paddingBottom: Math.max(insets.bottom, spacing.md),
      }}>
      <View style={{ paddingHorizontal: spacing.lg, paddingBottom: spacing.md, gap: spacing.xs }}>
        <ProdocLogo size={32} />
        <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
          {copy.adminCenter}
        </AppText>
      </View>
      {navList}
      {sidebarFooter}
    </View>
  );

  const header = (
    <View
      style={{
        borderBottomWidth: 1,
        borderBottomColor: theme.colors.border,
        backgroundColor: theme.colors.surface,
        paddingTop: desktop ? spacing.md : Math.max(insets.top, spacing.sm),
        paddingBottom: spacing.md,
        paddingHorizontal: tablet ? spacing.xl : spacing.lg,
        gap: spacing.md,
      }}>
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.md }}>
        {showBack && onBack ? (
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={copy.back}
            onPress={handleBack}
            hitSlop={10}
            style={{
              width: 40,
              height: 40,
              borderRadius: 12,
              alignItems: 'center',
              justifyContent: 'center',
            }}>
            <IconBack size={22} color={theme.colors.text} />
          </Pressable>
        ) : null}
        {!desktop ? (
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Open menu"
            onPress={() => setDrawerOpen(true)}
            hitSlop={10}
            style={{
              width: 40,
              height: 40,
              borderRadius: 12,
              borderWidth: 1,
              borderColor: theme.colors.border,
              alignItems: 'center',
              justifyContent: 'center',
              backgroundColor: theme.colors.background,
            }}>
            <AdminIconMenu color={theme.colors.text} />
          </Pressable>
        ) : null}
        <View style={{ flex: 1, gap: 2 }}>
          <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
            {copy.adminCenter} / {title}
          </AppText>
          <AppText variant="title" style={{ fontSize: 22, lineHeight: 28, color: theme.colors.text }}>
            {title}
          </AppText>
          {subtitle ? (
            <AppText variant="muted" style={{ fontSize: 13, lineHeight: 18 }} numberOfLines={2}>
              {subtitle}
            </AppText>
          ) : null}
        </View>
        <Pressable
          accessibilityRole="button"
          accessibilityLabel={copy.adminProfileMenu}
          onPress={() => setProfileOpen(true)}
          style={{
            width: 40,
            height: 40,
            borderRadius: 20,
            backgroundColor: theme.colors.primaryLight,
            alignItems: 'center',
            justifyContent: 'center',
          }}>
          <AppText variant="label" style={{ color: theme.colors.primary }}>
            {(account?.fullName || account?.phoneNumber || 'A').trim().charAt(0).toUpperCase()}
          </AppText>
        </Pressable>
      </View>
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.sm }}>
        <View
          style={{
            flex: 1,
            height: 42,
            borderRadius: 12,
            borderWidth: 1,
            borderColor: theme.colors.border,
            backgroundColor: theme.colors.background,
            flexDirection: 'row',
            alignItems: 'center',
            paddingHorizontal: 12,
            gap: 8,
          }}>
          <AdminIconSearch size={16} color={theme.colors.textMuted} />
          <TextInput
            value={searchValue}
            onChangeText={onSearchChange}
            placeholder={copy.search}
            placeholderTextColor={theme.colors.textMuted}
            editable={Boolean(onSearchChange)}
            style={{
              flex: 1,
              color: theme.colors.text,
              fontSize: 14,
              paddingVertical: 0,
            }}
          />
        </View>
        <NotificationBell href={'/notifications' as Href} accessibilityLabel={copy.notifications} />
      </View>
    </View>
  );

  return (
    <View style={{ flex: 1, backgroundColor: theme.colors.background, flexDirection: desktop ? 'row' : 'column' }}>
      {desktop ? sidebar : null}
      <View style={{ flex: 1 }}>
        {header}
        <FormScreenScroll
          contentContainerStyle={{
            paddingHorizontal: tablet ? spacing.xl : spacing.lg,
            paddingTop: spacing.lg,
            paddingBottom: Math.max(insets.bottom, spacing.xl) + 8,
            gap: spacing.lg,
          }}>
          {children}
        </FormScreenScroll>
      </View>

      {!desktop ? (
        <Modal visible={drawerOpen} animationType="fade" transparent onRequestClose={() => setDrawerOpen(false)}>
          <View style={{ flex: 1, flexDirection: 'row', backgroundColor: theme.colors.overlay }}>
            <View
              style={{
                width: Math.min(288, width * 0.82),
                backgroundColor: theme.colors.surface,
                paddingTop: Math.max(insets.top, spacing.md),
                paddingBottom: Math.max(insets.bottom, spacing.md),
              }}>
              <View style={{ paddingHorizontal: spacing.lg, paddingBottom: spacing.md, gap: spacing.xs }}>
                <ProdocLogo size={32} />
                <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
                  {copy.adminCenter}
                </AppText>
              </View>
              {navList}
              {sidebarFooter}
            </View>
            <Pressable style={{ flex: 1 }} onPress={() => setDrawerOpen(false)} />
          </View>
        </Modal>
      ) : null}

      <Modal visible={profileOpen} animationType="fade" transparent onRequestClose={() => setProfileOpen(false)}>
        <Pressable
          style={{ flex: 1, backgroundColor: theme.colors.overlay, justifyContent: 'flex-start', paddingTop: Math.max(insets.top, 56) }}
          onPress={() => setProfileOpen(false)}>
          <View
            style={{
              alignSelf: 'flex-end',
              marginRight: tablet ? spacing.xl : spacing.lg,
              width: 240,
              backgroundColor: theme.colors.surface,
              borderRadius: 16,
              borderWidth: 1,
              borderColor: theme.colors.border,
              padding: spacing.md,
              gap: spacing.sm,
            }}>
            <AppText variant="label">{copy.adminProfileMenu}</AppText>
            <AppText variant="muted" numberOfLines={1}>
              {account?.fullName || account?.phoneNumber || copy.adminAccountSettings}
            </AppText>
            {onLogout ? (
              <Pressable
                accessibilityRole="button"
                onPress={requestLogout}
                style={{
                  marginTop: spacing.sm,
                  paddingVertical: 12,
                  paddingHorizontal: 12,
                  borderRadius: 12,
                  backgroundColor: `${theme.colors.error}14`,
                }}>
                <AppText variant="label" style={{ color: theme.colors.error, textAlign: 'center' }}>
                  {copy.signOut}
                </AppText>
              </Pressable>
            ) : null}
          </View>
        </Pressable>
      </Modal>

      <Modal visible={logoutConfirmOpen} animationType="fade" transparent onRequestClose={() => setLogoutConfirmOpen(false)}>
        <View style={{ flex: 1, backgroundColor: theme.colors.overlay, alignItems: 'center', justifyContent: 'center', padding: spacing.xl }}>
          <View
            style={{
              width: '100%',
              maxWidth: 360,
              backgroundColor: theme.colors.surface,
              borderRadius: 18,
              borderWidth: 1,
              borderColor: theme.colors.border,
              padding: spacing.lg,
              gap: spacing.md,
            }}>
            <AppText variant="title" style={{ fontSize: 20, lineHeight: 26 }}>
              {copy.signOutConfirmTitle}
            </AppText>
            <AppText variant="muted">{copy.signOutConfirmBody}</AppText>
            <View style={{ flexDirection: 'row', gap: spacing.sm, marginTop: spacing.sm }}>
              <Pressable
                onPress={() => setLogoutConfirmOpen(false)}
                style={{
                  flex: 1,
                  paddingVertical: 12,
                  borderRadius: 12,
                  borderWidth: 1,
                  borderColor: theme.colors.border,
                  alignItems: 'center',
                }}>
                <AppText variant="label">{copy.cancel}</AppText>
              </Pressable>
              <Pressable
                onPress={confirmLogout}
                style={{
                  flex: 1,
                  paddingVertical: 12,
                  borderRadius: 12,
                  backgroundColor: theme.colors.error,
                  alignItems: 'center',
                }}>
                <AppText variant="label" style={{ color: '#fff' }}>
                  {copy.signOutConfirmAction}
                </AppText>
              </Pressable>
            </View>
          </View>
        </View>
      </Modal>
    </View>
  );
}

export function AdminPanel({
  title,
  description,
  action,
  children,
}: {
  title: string;
  description?: string;
  action?: ReactNode;
  children: ReactNode;
}) {
  const theme = useProdocTheme();
  return (
    <View
      style={{
        backgroundColor: theme.colors.surface,
        borderRadius: radius.lg,
        borderWidth: 1,
        borderColor: theme.colors.border,
        padding: spacing.lg,
        gap: spacing.md,
        shadowColor: '#14243D',
        shadowOffset: { width: 0, height: 4 },
        shadowOpacity: theme.scheme === 'light' ? 0.04 : 0,
        shadowRadius: 12,
        elevation: theme.scheme === 'light' ? 1 : 0,
      }}>
      <View style={{ flexDirection: 'row', alignItems: 'flex-start', justifyContent: 'space-between', gap: spacing.md }}>
        <View style={{ flex: 1, gap: 2 }}>
          <AppText variant="label" style={{ color: theme.colors.text }}>
            {title}
          </AppText>
          {description ? (
            <AppText variant="muted" style={{ fontSize: 13, lineHeight: 18 }}>
              {description}
            </AppText>
          ) : null}
        </View>
        {action}
      </View>
      {children}
    </View>
  );
}

export function AdminSkeleton({ height = 88 }: { height?: number }) {
  const theme = useProdocTheme();
  return (
    <View
      style={{
        height,
        borderRadius: radius.lg,
        backgroundColor: theme.colors.border,
        opacity: 0.45,
      }}
    />
  );
}
