import axios from 'axios';

import { api, refreshSession } from '@/services/api/client';
import { persistSession, useSessionStore } from '@/stores/session-store';

async function withAuthRetry<T>(run: () => Promise<T>): Promise<T> {
  try {
    return await run();
  } catch (error) {
    if (!axios.isAxiosError(error) || error.response?.status !== 403) {
      throw error;
    }
    const refreshToken = useSessionStore.getState().refreshToken;
    if (!refreshToken) {
      throw error;
    }
    await persistSession(await refreshSession(refreshToken));
    return run();
  }
}

export type ReferralItem = {
  id: string;
  status: string;
  displayStatus: string;
  referredLabel: string;
  registeredAtUtc: string;
  convertedAtUtc: string | null;
  rewardedAtUtc: string | null;
  rewardPointsCredited: number | null;
};

export type ReferralDashboard = {
  code: string;
  shareUrl: string;
  whatsAppShareText: string;
  registeredCount: number;
  convertedCount: number;
  rewardedCount: number;
  pointsBalance: number;
  creditBalanceInr: number;
  recentReferrals: ReferralItem[];
};

export type RewardLedgerItem = {
  id: string;
  entryType: string;
  pointsDelta: number;
  creditDeltaInr: number;
  reason: string;
  referenceType: string | null;
  referenceId: string | null;
  createdAtUtc: string;
  expiresAtUtc: string | null;
};

export type RewardWallet = {
  pointsBalance: number;
  creditBalanceInr: number;
  pointsEarnedTotal: number;
  pointsRedeemedTotal: number;
  pointsPerInr: number;
  redeemableCreditValueInr: number;
  history: RewardLedgerItem[];
};

export type CouponDetails = {
  id: string;
  code: string;
  name: string;
  discountType: string;
  discountValue: number;
  minBookingAmount: number | null;
  maxDiscountAmount: number | null;
  firstBookingOnly: boolean;
  newUserOnly: boolean;
  endsAtUtc: string;
  isActive: boolean;
  redemptionCount: number;
  userRedemptionCount?: number;
  alreadyUsed?: boolean;
  eligibleForUser?: boolean;
  eligibilityNote?: string | null;
};

export type CouponPreview = {
  code: string;
  bookingAmount: number;
  discountAmount: number;
  payableAmount: number;
  message: string;
};

export type MarketingConsent = {
  marketingOptIn: boolean;
  updatedAtUtc: string;
};

export type CampaignDetails = {
  id: string;
  name: string;
  kind: string;
  title: string;
  body: string;
  isActive: boolean;
  deliveryCount: number;
};

export type GrowthSeriesPoint = { period: string; count: number; amount: number };
export type CouponUsageRow = { code: string; name: string; redemptions: number; discountTotal: number };

export type GrowthDashboard = {
  newRegistrations30d: number;
  referralRegistrations: number;
  referralConversions: number;
  referralRewardsPaid: number;
  referralConversionRate: number;
  couponsRedeemed30d: number;
  couponDiscountGiven30d: number;
  outstandingRewardPoints: number;
  outstandingRewardCreditsInr: number;
  repeatBookers30d: number;
  returningPatients90d: number;
  patientRetentionRate90d: number;
  campaignDeliveries30d: number;
  registrationTrend: GrowthSeriesPoint[];
  referralConversionTrend: GrowthSeriesPoint[];
  topCoupons: CouponUsageRow[];
};

export function getReferralDashboard() {
  return withAuthRetry(async () => (await api.get<ReferralDashboard>('/api/v1/growth/referral')).data);
}

export function getRewardWallet() {
  return withAuthRetry(async () => (await api.get<RewardWallet>('/api/v1/growth/wallet')).data);
}

export function redeemRewardPoints(points: number, idempotencyKey?: string) {
  return withAuthRetry(
    async () =>
      (
        await api.post<RewardWallet>('/api/v1/growth/wallet/redeem', {
          points,
          idempotencyKey: idempotencyKey || undefined,
        })
      ).data,
  );
}

export function createAdminCoupon(input: {
  code: string;
  name: string;
  discountType: string;
  discountValue: number;
  startsAtUtc: string;
  endsAtUtc: string;
  minBookingAmount?: number | null;
  maxDiscountAmount?: number | null;
  firstBookingOnly?: boolean;
  newUserOnly?: boolean;
  maxRedemptions?: number | null;
  maxRedemptionsPerUser?: number | null;
}) {
  return withAuthRetry(async () => (await api.post<CouponDetails>('/api/v1/admin/growth/coupons', input)).data);
}

export function listGrowthCoupons() {
  return withAuthRetry(async () => (await api.get<CouponDetails[]>('/api/v1/growth/coupons')).data);
}

export function previewCoupon(appointmentId: string, couponCode: string) {
  return withAuthRetry(
    async () => (await api.post<CouponPreview>('/api/v1/growth/coupons/preview', { appointmentId, couponCode })).data,
  );
}

export function getMarketingConsent() {
  return withAuthRetry(async () => (await api.get<MarketingConsent>('/api/v1/growth/marketing-consent')).data);
}

export function setMarketingConsent(marketingOptIn: boolean) {
  return withAuthRetry(
    async () => (await api.put<MarketingConsent>('/api/v1/growth/marketing-consent', { marketingOptIn })).data,
  );
}

export function getGrowthDashboard() {
  return withAuthRetry(async () => (await api.get<GrowthDashboard>('/api/v1/admin/growth/dashboard')).data);
}

export function listAdminCampaigns() {
  return withAuthRetry(async () => (await api.get<CampaignDetails[]>('/api/v1/admin/growth/campaigns')).data);
}

export function createAdminCampaign(input: { name: string; kind: string; title: string; body: string }) {
  return withAuthRetry(async () => (await api.post<CampaignDetails>('/api/v1/admin/growth/campaigns', input)).data);
}

export function setCampaignActive(campaignId: string, isActive: boolean) {
  return withAuthRetry(
    async () => (await api.post<CampaignDetails>(`/api/v1/admin/growth/campaigns/${campaignId}/active`, { isActive })).data,
  );
}

export function listAdminCoupons() {
  return withAuthRetry(async () => (await api.get<CouponDetails[]>('/api/v1/admin/growth/coupons')).data);
}

export function setCouponActive(couponId: string, isActive: boolean) {
  return withAuthRetry(
    async () => (await api.post<CouponDetails>(`/api/v1/admin/growth/coupons/${couponId}/active`, { isActive })).data,
  );
}
