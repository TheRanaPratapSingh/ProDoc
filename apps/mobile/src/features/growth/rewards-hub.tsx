import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import * as Clipboard from 'expo-clipboard';
import { useMemo, useState } from 'react';
import { Alert, Linking, Pressable, Share, View } from 'react-native';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { EmptyState } from '@/components/ui/empty-state';
import { ErrorState } from '@/components/ui/error-state';
import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { useCopy } from '@/i18n/copy';
import {
  getMarketingConsent,
  getReferralDashboard,
  getRewardWallet,
  listGrowthCoupons,
  redeemRewardPoints,
  setMarketingConsent,
} from '@/services/api/growth-api';
import { notifySuccess } from '@/stores/feedback-store';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

export function RewardsHub() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const queryClient = useQueryClient();
  const [redeemPoints, setRedeemPoints] = useState('10');
  const [error, setError] = useState<string | null>(null);
  const [redeemKey] = useState(() => `ui-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`);

  const referral = useQuery({ queryKey: ['growth', 'referral'], queryFn: getReferralDashboard });
  const wallet = useQuery({ queryKey: ['growth', 'wallet'], queryFn: getRewardWallet });
  const coupons = useQuery({ queryKey: ['growth', 'coupons'], queryFn: listGrowthCoupons });
  const consent = useQuery({ queryKey: ['growth', 'consent'], queryFn: getMarketingConsent });

  const pointsPerInr = wallet.data?.pointsPerInr || 10;
  const redeemPreviewInr = useMemo(() => {
    const points = Number(redeemPoints) || 0;
    if (points <= 0 || points % pointsPerInr !== 0) return null;
    return points / pointsPerInr;
  }, [pointsPerInr, redeemPoints]);

  const redeem = useMutation({
    mutationFn: (points: number) => redeemRewardPoints(points, `${redeemKey}-${points}`),
    onSuccess: async (result) => {
      setError(null);
      notifySuccess(`Redeemed. Wallet now ${result.pointsBalance} pts · ₹${result.creditBalanceInr.toFixed(0)}`);
      await queryClient.invalidateQueries({ queryKey: ['growth'] });
    },
    onError: (err) => setError(problemMessage(err, copy.redeemNow)),
  });

  const consentMutation = useMutation({
    mutationFn: (optIn: boolean) => setMarketingConsent(optIn),
    onSuccess: async () => queryClient.invalidateQueries({ queryKey: ['growth', 'consent'] }),
    onError: (err) => setError(problemMessage(err, copy.marketingConsent)),
  });

  const whatsappUrl = useMemo(() => {
    const text = referral.data?.whatsAppShareText ?? '';
    return `https://wa.me/?text=${encodeURIComponent(text)}`;
  }, [referral.data?.whatsAppShareText]);

  const copyCode = async () => {
    const code = referral.data?.code;
    if (!code) return;
    await Clipboard.setStringAsync(code);
    notifySuccess('Referral code copied');
  };

  const copyLink = async () => {
    const url = referral.data?.shareUrl;
    if (!url) return;
    await Clipboard.setStringAsync(url);
    notifySuccess('Invite link copied');
  };

  const copyCoupon = async (code: string) => {
    await Clipboard.setStringAsync(code);
    notifySuccess(`Coupon ${code} copied`);
  };

  const shareInvite = async () => {
    if (!referral.data?.whatsAppShareText) return;
    await Share.share({
      message: referral.data.whatsAppShareText,
      url: referral.data.shareUrl,
    });
  };

  const shareWhatsApp = async () => {
    if (!referral.data?.whatsAppShareText) return;
    try {
      const canOpen = await Linking.canOpenURL(whatsappUrl);
      if (canOpen) {
        await Linking.openURL(whatsappUrl);
        return;
      }
    } catch {
      // fall through to native share
    }
    await shareInvite();
  };

  const confirmRedeem = () => {
    const points = Number(redeemPoints) || 0;
    if (points <= 0 || points % pointsPerInr !== 0) {
      setError(`Enter points in multiples of ${pointsPerInr}.`);
      return;
    }
    if ((wallet.data?.pointsBalance ?? 0) < points) {
      setError('Insufficient reward balance.');
      return;
    }
    Alert.alert(
      'Redeem to credit',
      `Convert ${points} points into ₹${(points / pointsPerInr).toFixed(0)} ProDoc booking credit?`,
      [
        { text: 'Cancel', style: 'cancel' },
        { text: 'Redeem', onPress: () => redeem.mutate(points) },
      ],
    );
  };

  const loading = referral.isLoading || wallet.isLoading;
  const loadError = referral.error ?? wallet.error;

  return (
    <AuthScaffold>
      <View style={{ gap: spacing.md }}>
        <ScreenHeader title={copy.growthTitle} showBack />
        <AppText variant="muted">{copy.growthBody}</AppText>

        {loading && !referral.data ? (
          <Card>
            <AppText variant="muted">Loading your referral code and wallet…</AppText>
          </Card>
        ) : null}

        {loadError ? (
          <ErrorState
            title="Unable to load rewards"
            body={problemMessage(loadError, 'Check your connection and try again.')}
            actionLabel="Retry"
            onAction={() => {
              void referral.refetch();
              void wallet.refetch();
              void coupons.refetch();
            }}
          />
        ) : null}

        {referral.data ? (
          <Card>
            <AppText variant="label">{copy.referralCode}</AppText>
            <AppText variant="title" style={{ letterSpacing: 1 }}>
              {referral.data.code}
            </AppText>
            <AppText variant="muted">{referral.data.shareUrl}</AppText>
            <AppText variant="muted">
              {copy.referralStats
                .replace('{registered}', String(referral.data.registeredCount))
                .replace('{converted}', String(referral.data.convertedCount))
                .replace('{rewarded}', String(referral.data.rewardedCount))}
            </AppText>
            <View style={{ flexDirection: 'row', gap: spacing.sm }}>
              <View style={{ flex: 1 }}>
                <Button label="Copy code" variant="secondary" onPress={() => void copyCode()} />
              </View>
              <View style={{ flex: 1 }}>
                <Button label="Copy link" variant="secondary" onPress={() => void copyLink()} />
              </View>
            </View>
            <Button label={copy.shareReferral} onPress={() => void shareInvite()} />
            <Button label={copy.shareWhatsApp} variant="secondary" onPress={() => void shareWhatsApp()} />
          </Card>
        ) : null}

        {referral.data && referral.data.recentReferrals.length > 0 ? (
          <Card>
            <AppText variant="label">Referral history</AppText>
            {referral.data.recentReferrals.map((item) => (
              <View key={item.id} style={{ gap: 2, paddingVertical: 6 }}>
                <AppText>
                  {item.referredLabel} · {item.displayStatus}
                </AppText>
                <AppText variant="muted">
                  {new Date(item.registeredAtUtc).toLocaleDateString()}
                  {item.rewardPointsCredited != null ? ` · +${item.rewardPointsCredited} pts` : ''}
                </AppText>
              </View>
            ))}
          </Card>
        ) : referral.data && !referral.isLoading ? (
          <Card>
            <AppText variant="label">Referral history</AppText>
            <EmptyState compact title="No referrals yet" body="Share your invite link to start tracking invites." />
          </Card>
        ) : null}

        {wallet.data ? (
          <Card>
            <AppText variant="label">{copy.rewardsWallet}</AppText>
            <AppText variant="title">
              {wallet.data.pointsBalance} {copy.points} · ₹{wallet.data.creditBalanceInr.toFixed(0)} credit
            </AppText>
            <AppText variant="muted">
              Earned {wallet.data.pointsEarnedTotal} · Redeemed {wallet.data.pointsRedeemedTotal} · {wallet.data.pointsPerInr}{' '}
              pts = ₹1
            </AppText>
            <AppText variant="muted">
              Available to redeem ≈ ₹{wallet.data.redeemableCreditValueInr.toFixed(0)} booking credit
            </AppText>
            <TextField
              label={copy.redeemPoints}
              value={redeemPoints}
              onChangeText={setRedeemPoints}
              keyboardType="number-pad"
            />
            {redeemPreviewInr != null ? (
              <AppText variant="muted">You will receive ₹{redeemPreviewInr.toFixed(0)} ProDoc credit</AppText>
            ) : null}
            <Button
              label={copy.redeemNow}
              onPress={confirmRedeem}
              loading={redeem.isPending}
              disabled={redeem.isPending || !wallet.data.pointsBalance}
            />
            {(wallet.data.history ?? []).slice(0, 8).map((item) => (
              <View key={item.id} style={{ gap: 2 }}>
                <AppText>
                  {item.entryType} · {item.pointsDelta} pts · ₹{item.creditDeltaInr.toFixed(0)}
                </AppText>
                <AppText variant="muted">{item.reason}</AppText>
              </View>
            ))}
          </Card>
        ) : null}

        <Card>
          <AppText variant="label">{copy.availableCoupons}</AppText>
          {coupons.isLoading && !coupons.data ? <AppText variant="muted">Loading coupons…</AppText> : null}
          {coupons.isError ? (
            <AppText style={{ color: theme.colors.error }}>
              {problemMessage(coupons.error, 'Unable to load coupons.')}
            </AppText>
          ) : null}
          {(coupons.data ?? []).map((coupon) => (
            <View key={coupon.id} style={{ gap: 4, paddingVertical: 6 }}>
              <AppText>
                {coupon.code} · {coupon.name}
              </AppText>
              <AppText variant="muted">
                {coupon.discountType === 'Percent' ? `${coupon.discountValue}%` : `₹${coupon.discountValue}`}
                {coupon.minBookingAmount != null ? ` · min ₹${coupon.minBookingAmount}` : ''}
                {coupon.maxDiscountAmount != null ? ` · max ₹${coupon.maxDiscountAmount}` : ''}
                {coupon.firstBookingOnly ? ' · first booking' : ''}
                {' · '}
                {copy.expiresOn} {new Date(coupon.endsAtUtc).toLocaleDateString()}
              </AppText>
              <AppText
                variant="muted"
                style={{ color: coupon.eligibleForUser === false ? theme.colors.error : theme.colors.primary }}>
                {coupon.alreadyUsed
                  ? 'Already used'
                  : coupon.eligibleForUser === false
                    ? coupon.eligibilityNote || 'Not currently eligible'
                    : 'Eligible for you'}
              </AppText>
              <Button label="Copy coupon" variant="secondary" onPress={() => void copyCoupon(coupon.code)} />
            </View>
          ))}
          {!coupons.isLoading && (coupons.data ?? []).length === 0 ? (
            <AppText variant="muted">{copy.noCoupons}</AppText>
          ) : null}
          <AppText variant="muted">Apply coupons during appointment payment checkout.</AppText>
        </Card>

        <Card>
          <AppText variant="label">{copy.marketingConsent}</AppText>
          <AppText variant="muted">{copy.marketingConsentBody}</AppText>
          <Pressable onPress={() => consentMutation.mutate(!(consent.data?.marketingOptIn ?? false))}>
            <AppText style={{ color: theme.colors.primary }}>
              {consent.data?.marketingOptIn ? copy.marketingOptOut : copy.marketingOptIn}
            </AppText>
          </Pressable>
        </Card>

        {error ? <AppText style={{ color: theme.colors.error }}>{error}</AppText> : null}
      </View>
    </AuthScaffold>
  );
}
