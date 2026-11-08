import { AppText } from '@/components/ui/text';
import { useProdocTheme } from '@/theme/prodoc-theme';

export function FormFieldError({ message }: { message?: string | null }) {
  const theme = useProdocTheme();
  if (!message) {
    return null;
  }

  return (
    <AppText
      variant="muted"
      accessibilityLiveRegion="polite"
      accessibilityRole="alert"
      style={{ color: theme.colors.error, fontSize: 13, lineHeight: 18 }}>
      {message}
    </AppText>
  );
}
