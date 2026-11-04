import { getLocales } from 'expo-localization';
import { create } from 'zustand';

export type Language = 'en' | 'hi';
export type ColorPreference = 'system' | 'light' | 'dark';

function initialLanguage(): Language {
  const code = getLocales()[0]?.languageCode;
  return code === 'hi' ? 'hi' : 'en';
}

type UiState = {
  language: Language;
  colorScheme: ColorPreference;
  setLanguage: (language: Language) => void;
  setColorScheme: (colorScheme: ColorPreference) => void;
};

export const useUiStore = create<UiState>((set) => ({
  language: initialLanguage(),
  colorScheme: 'system',
  setLanguage: (language) => set({ language }),
  setColorScheme: (colorScheme) => set({ colorScheme }),
}));
