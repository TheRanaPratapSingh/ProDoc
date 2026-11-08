import { useMemo, useState } from 'react';
import { Pressable, ScrollView, View } from 'react-native';

import { AppText } from '@/components/ui/text';
import { TextField } from '@/components/ui/text-field';
import { useProdocTheme } from '@/theme/prodoc-theme';
import { radius, spacing } from '@/theme/tokens';

export type SearchableOption = {
  id: string;
  label: string;
  subtitle?: string | null;
};

type SearchableMultiSelectProps = {
  label: string;
  options: SearchableOption[];
  selectedIds: string[];
  onChange: (ids: string[]) => void;
  placeholder?: string;
  emptyMessage?: string;
  multiple?: boolean;
  disabled?: boolean;
};

/** Searchable single/multi select for admin association pickers. */
export function SearchableMultiSelect({
  label,
  options,
  selectedIds,
  onChange,
  placeholder = 'Search…',
  emptyMessage = 'No options available.',
  multiple = true,
  disabled = false,
}: SearchableMultiSelectProps) {
  const theme = useProdocTheme();
  const [query, setQuery] = useState('');
  const [open, setOpen] = useState(false);

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return options;
    return options.filter(
      (item) =>
        item.label.toLowerCase().includes(q) ||
        item.id.toLowerCase().includes(q) ||
        (item.subtitle ?? '').toLowerCase().includes(q),
    );
  }, [options, query]);

  const selected = options.filter((item) => selectedIds.includes(item.id));

  const toggle = (id: string) => {
    if (disabled) return;
    if (!multiple) {
      onChange(selectedIds.includes(id) ? [] : [id]);
      setOpen(false);
      setQuery('');
      return;
    }
    onChange(selectedIds.includes(id) ? selectedIds.filter((item) => item !== id) : [...selectedIds, id]);
  };

  return (
    <View style={{ gap: spacing.xs, maxWidth: '100%' }}>
      <AppText variant="label" style={{ fontSize: 13, lineHeight: 18 }}>
        {label}
      </AppText>

      {selected.length > 0 ? (
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.xs }}>
          {selected.map((item) => (
            <Pressable
              key={item.id}
              disabled={disabled}
              onPress={() => toggle(item.id)}
              style={{
                paddingHorizontal: 10,
                paddingVertical: 6,
                borderRadius: radius.pill,
                backgroundColor: theme.colors.primaryLight,
                borderWidth: 1,
                borderColor: theme.colors.primary,
                maxWidth: '100%',
              }}>
              <AppText variant="label" style={{ color: theme.colors.primary, fontSize: 12 }} numberOfLines={1}>
                {item.label} ×
              </AppText>
            </Pressable>
          ))}
        </View>
      ) : null}

      <TextField
        label=""
        value={query}
        onChangeText={(value) => {
          setQuery(value);
          setOpen(true);
        }}
        onFocus={() => setOpen(true)}
        placeholder={placeholder}
        editable={!disabled}
      />

      {open ? (
        <View
          style={{
            borderWidth: 1,
            borderColor: theme.colors.border,
            borderRadius: radius.lg,
            backgroundColor: theme.colors.surface,
            maxHeight: 220,
            overflow: 'hidden',
          }}>
          {options.length === 0 ? (
            <View style={{ padding: spacing.md }}>
              <AppText variant="muted">{emptyMessage}</AppText>
            </View>
          ) : (
            <ScrollView keyboardShouldPersistTaps="handled" nestedScrollEnabled>
              {filtered.map((item) => {
                const isSelected = selectedIds.includes(item.id);
                return (
                  <Pressable
                    key={item.id}
                    onPress={() => toggle(item.id)}
                    style={{
                      paddingHorizontal: spacing.md,
                      paddingVertical: spacing.md,
                      borderBottomWidth: 1,
                      borderBottomColor: theme.colors.border,
                      backgroundColor: isSelected ? theme.colors.primaryLight : theme.colors.surface,
                      gap: 2,
                    }}>
                    <AppText variant="label" style={{ color: theme.colors.text }}>
                      {item.label}
                    </AppText>
                    <AppText variant="caption" style={{ color: theme.colors.textMuted }}>
                      {item.subtitle ?? item.id}
                    </AppText>
                  </Pressable>
                );
              })}
              {filtered.length === 0 ? (
                <View style={{ padding: spacing.md }}>
                  <AppText variant="muted">No matches for “{query.trim()}”.</AppText>
                </View>
              ) : null}
            </ScrollView>
          )}
          <Pressable onPress={() => setOpen(false)} style={{ padding: spacing.sm, alignItems: 'center' }}>
            <AppText variant="label" style={{ color: theme.colors.primary }}>
              Done
            </AppText>
          </Pressable>
        </View>
      ) : null}
    </View>
  );
}
