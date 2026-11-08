import { View } from 'react-native';

import { DoctorShell } from '@/components/layout/doctor-shell';
import { AuthScaffold } from '@/components/layout/auth-scaffold';
import { ScreenHeader } from '@/components/layout/responsive-header';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { AppText } from '@/components/ui/text';
import { useCopy } from '@/i18n/copy';
import { useUiStore } from '@/stores/ui-store';
import { spacing } from '@/theme/tokens';

export function DoctorSettingsScreen() {
  const copy = useCopy();
  const language = useUiStore((state) => state.language);
  const colorScheme = useUiStore((state) => state.colorScheme);
  const setLanguage = useUiStore((state) => state.setLanguage);
  const setColorScheme = useUiStore((state) => state.setColorScheme);

  return (
    <DoctorShell active="settings">
      <AuthScaffold>
        <ScreenHeader title={copy.doctorSettingsTitle} />
        <Card style={{ gap: spacing.md }}>
          <AppText variant="label">{copy.doctorSettingsLanguage}</AppText>
          <View style={{ flexDirection: 'row', gap: spacing.sm }}>
            <Button label="English" variant={language === 'en' ? 'primary' : 'secondary'} onPress={() => setLanguage('en')} />
            <Button label="हिन्दी" variant={language === 'hi' ? 'primary' : 'secondary'} onPress={() => setLanguage('hi')} />
          </View>
        </Card>
        <Card style={{ gap: spacing.md }}>
          <AppText variant="label">{copy.doctorSettingsTheme}</AppText>
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
            <Button
              label="System"
              variant={colorScheme === 'system' ? 'primary' : 'secondary'}
              onPress={() => setColorScheme('system')}
            />
            <Button
              label="Light"
              variant={colorScheme === 'light' ? 'primary' : 'secondary'}
              onPress={() => setColorScheme('light')}
            />
            <Button
              label="Dark"
              variant={colorScheme === 'dark' ? 'primary' : 'secondary'}
              onPress={() => setColorScheme('dark')}
            />
          </View>
        </Card>
      </AuthScaffold>
    </DoctorShell>
  );
}
