import { View } from 'react-native';

import { Button } from '@/components/ui/button';
import { AppText } from '@/components/ui/text';
import { formatFollowUpDate, type FollowUpDetails } from '@/services/api/follow-up-api';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { spacing } from '@/theme/tokens';

type Props = {
  item: FollowUpDetails;
  bookLabel?: string;
  onBook?: () => void;
  footer?: React.ReactNode;
};

export function FollowUpCard({ item, bookLabel, onBook, footer }: Props) {
  const theme = useProdocTheme();
  const open = item.status === 'Recommended' || item.status === 'AppointmentBooked';
  const showBook = open && item.requiresBooking && !item.hasBookedAppointment && onBook;

  return (
    <View
      style={{
        backgroundColor: theme.colors.surface,
        borderRadius: 16,
        borderWidth: 1,
        borderColor: theme.colors.border,
        padding: spacing.lg,
        gap: spacing.sm,
      }}>
      <View style={{ flexDirection: 'row', justifyContent: 'space-between', gap: spacing.sm }}>
        <AppText variant="label" style={{ color: theme.colors.primary, flex: 1 }}>
          {item.displayStatus === 'Due' || item.displayStatus === 'Overdue'
            ? 'Your Follow-up is Due'
            : 'Follow-up Recommendation'}
        </AppText>
        <AppText variant="muted" style={{ fontSize: 12 }}>
          {item.displayStatus}
        </AppText>
      </View>
      <AppText variant="title" style={{ fontSize: 18, lineHeight: 24 }}>
        {item.doctorName}
      </AppText>
      <AppText variant="muted">{item.clinicName}</AppText>
      <AppText variant="body">Date: {formatFollowUpDate(item.recommendedDate)}</AppText>
      {item.followUpNote ? (
        <AppText variant="muted" style={{ lineHeight: 20 }}>
          Note: {item.followUpNote}
        </AppText>
      ) : null}
      {item.hasBookedAppointment ? (
        <AppText variant="label" style={{ color: theme.colors.primary }}>
          Appointment booked
        </AppText>
      ) : null}
      {showBook ? <Button label={bookLabel ?? 'Book Follow-up Appointment'} onPress={onBook} /> : null}
      {footer}
    </View>
  );
}
