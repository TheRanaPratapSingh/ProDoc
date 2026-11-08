import { View } from 'react-native';

import { AppText } from '@/components/ui/text';
import { useProdocTheme } from '@/theme/prodoc-theme';

/** Consistent mandatory asterisk + label used across ProDoc forms. */
export function FieldLabel({
  label,
  required = false,
  optionalHint,
}: {
  label: string;
  required?: boolean;
  optionalHint?: string;
}) {
  const theme = useProdocTheme();
  return (
    <View style={{ flexDirection: 'row', alignItems: 'center', gap: 4, flexWrap: 'wrap' }}>
      <AppText variant="label" style={{ fontSize: 13, lineHeight: 18 }}>
        {label}
      </AppText>
      {required ? (
        <AppText variant="label" style={{ fontSize: 13, lineHeight: 18, color: theme.colors.error }}>
          *
        </AppText>
      ) : optionalHint ? (
        <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
          ({optionalHint})
        </AppText>
      ) : null}
    </View>
  );
}
