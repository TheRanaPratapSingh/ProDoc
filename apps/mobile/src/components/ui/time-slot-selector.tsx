import { View } from 'react-native';

import { TimeSlot } from '@/components/ui/time-slot';
import { AppText } from '@/components/ui/text';
import { spacing } from '@/theme/tokens';

export type SlotOption = {
  start: string;
  end?: string;
  label?: string;
  disabled?: boolean;
};

/** Wrap of TimeSlot chips for booking / reschedule flows. */
export function TimeSlotSelector({
  slots,
  selectedStart,
  onSelect,
  emptyLabel,
  busy = false,
}: {
  slots: SlotOption[];
  selectedStart?: string | null;
  onSelect: (start: string) => void;
  emptyLabel?: string;
  busy?: boolean;
}) {
  if (slots.length === 0) {
    return emptyLabel ? <AppText variant="muted">{emptyLabel}</AppText> : null;
  }

  return (
    <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm, width: '100%', maxWidth: '100%' }}>
      {slots.map((slot) => {
        const label = slot.label ?? slot.start.slice(0, 5);
        return (
          <TimeSlot
            key={slot.start}
            label={label}
            selected={selectedStart === slot.start}
            disabled={busy || slot.disabled}
            onPress={() => onSelect(slot.start)}
          />
        );
      })}
    </View>
  );
}
