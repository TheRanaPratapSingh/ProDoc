import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import { Linking, Pressable, Share, View } from 'react-native';

import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
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
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';
import { problemMessage } from '@/utils/problem';

export function RewardsHub() {
  const copy = useCopy();
  const theme = useProdocTheme();
  const queryClient = useQueryClient();
  const [redeemPoints, setRedeemPoints] = useState('10');
  const [error, setError] = useState<string | null>(null);

  const referral = useQuery({ queryKey: ['growth', 'referral'], queryFn: getReferralDashboard });
  const wallet = useQuery({ queryKey: ['growth', 'wallet'], queryFn: getRewardWallet });
  const coupons = useQuery({ queryKey: ['growth', 'coupons'], queryFn: listGrowthCoupons });
  const consent = useQuery({ queryKey: ['growth', 'consent'], queryFn: getMarketingConsent });

  const redeem = useMutation({
    mutationFn: (points: number) => redeemRewardPoints(points),
    onSuccess: async () => {
      setError(null);
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

  return (
    <AuthScaffold>
      <View style={{ gap: spacing.md }}>
        <AppText variant="title">{copy.growthTitle}</AppText>
        <AppText variant="muted">{copy.growthBody}</AppText>

        <Card>
          <AppText variant="label">{copy.referralCode}</AppText>
          <AppText variant="title">{referral.data?.code ?? '—'}</AppText>
          <AppText variant="muted">{referral.data?.shareUrl}</AppText>
          <AppText variant="muted">
            {copy.referralStats
              .replace('{registered}', String(referral.data?.registeredCount ?? 0))
              .replace('{converted}', String(referral.data?.convertedCount ?? 0))
              .replace('{rewarded}', String(referral.data?.rewardedCount ?? 0))}
          </AppText>
          <Button
            label={copy.shareReferral}
            onPress={() => {
              void Share.share({
                message: referral.data?.whatsAppShareText ?? '',
                url: referral.data?.shareUrl,
              });
            }}
          />
          <Button label={copy.shareWhatsApp} variant="secondary" onPress={() => void Linking.openURL(whatsappUrl)} />
        </Card>

        <Card>
          <AppText variant="label">{copy.rewardsWallet}</AppText>
          <AppText variant="title">
            {wallet.data?.pointsBalance ?? 0} {copy.points} · ₹{(wallet.data?.creditBalanceInr ?? 0).toFixed(0)}
          </AppText>
          <TextField label={copy.redeemPoints} value={redeemPoints} onChangeText={setRedeemPoints} keyboardType="number-pad" />
          <Button
            label={copy.redeemNow}
            onPress={() => redeem.mutate(Number(redeemPoints) || 0)}
            loading={redeem.isPending}
          />
          {(wallet.data?.history ?? []).slice(0, 8).map((item) => (
            <View key={item.id} style={{ gap: 2 }}>
              <AppText>
                {item.entryType} · {item.pointsDelta} pts · ₹{item.creditDeltaInr.toFixed(0)}
              </AppText>
              <AppText variant="muted">{item.reason}</AppText>
            </View>
          ))}
        </Card>

        <Card>
          <AppText variant="label">{copy.availableCoupons}</AppText>
          {(coupons.data ?? []).map((coupon) => (
            <View key={coupon.id} style={{ gap: 2 }}>
              <AppText>
                {coupon.code} · {coupon.name}
              </AppText>
              <AppText variant="muted">
                {coupon.discountType === 'Percent' ? `${coupon.discountValue}%` : `₹${coupon.discountValue}`} ·{' '}
                {copy.expiresOn} {new Date(coupon.endsAtUtc).toLocaleDateString()}
              </AppText>
            </View>
          ))}
          {(coupons.data ?? []).length === 0 ? <AppText variant="muted">{copy.noCoupons}</AppText> : null}
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
