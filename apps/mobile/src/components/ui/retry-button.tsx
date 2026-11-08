import { Button } from '@/components/ui/button';

export function RetryButton({
  label = 'Try again',
  loading = false,
  disabled = false,
  onPress,
}: {
  label?: string;
  loading?: boolean;
  disabled?: boolean;
  onPress: () => void;
}) {
  return (
    <Button
      label={label}
      loading={loading}
      disabled={disabled || loading}
      onPress={() => {
        if (!loading && !disabled) {
          onPress();
        }
      }}
    />
  );
}
