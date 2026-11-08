import { useState } from 'react';
import { View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { DoctorCalendarScreen } from '@/features/appointments/doctor-calendar';
import { ScheduleEditor } from '@/features/providers/schedule-editor';
import { DoctorShell } from '@/components/layout/doctor-shell';
import { Segmented } from '@/components/ui/segmented';
import { useCopy } from '@/i18n/copy';
import { spacing } from '@/theme/tokens';

type HubTab = 'calendar' | 'hours' | 'leave';

/** Schedule tab: calendar + shared availability editor (same APIs as Reception). */
export function DoctorScheduleHub() {
  const copy = useCopy();
  const insets = useSafeAreaInsets();
  const [tab, setTab] = useState<HubTab>('calendar');

  return (
    <DoctorShell active="schedule">
      <View style={{ flex: 1 }}>
        <View
          style={{
            paddingHorizontal: spacing.md,
            paddingTop: Math.max(insets.top, spacing.sm),
            paddingBottom: spacing.sm,
            width: '100%',
            maxWidth: '100%',
          }}>
          <Segmented
            value={tab}
            onChange={setTab}
            options={[
              { value: 'calendar', label: copy.doctorCalendarTitle },
              { value: 'hours', label: copy.doctorWorkingDays },
              { value: 'leave', label: copy.doctorLeaveTab },
            ]}
          />
        </View>
        <View style={{ flex: 1, minHeight: 0 }}>
          {tab === 'calendar' ? <DoctorCalendarScreen embedded /> : null}
          {tab === 'hours' ? <ScheduleEditor key="hours" surface="doctor" initialTab="working" embedded lockTab /> : null}
          {tab === 'leave' ? <ScheduleEditor key="leave" surface="doctor" initialTab="leave" embedded lockTab /> : null}
        </View>
      </View>
    </DoctorShell>
  );
}
