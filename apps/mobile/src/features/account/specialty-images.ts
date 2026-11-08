import type { ImageSource } from 'expo-image';

import type { Specialization } from '@/services/api/provider-api';

/** Bundled specialty photography — keyed by backend specialization code. */
const specialtyImages: Record<string, ImageSource> = {
  'general-medicine': require('../../../assets/images/specialties/general-medicine.jpg'),
  cardiology: require('../../../assets/images/specialties/cardiology.jpg'),
  dermatology: require('../../../assets/images/specialties/dermatology.jpg'),
  pediatrics: require('../../../assets/images/specialties/pediatrics.jpg'),
  orthopedics: require('../../../assets/images/specialties/orthopedics.jpg'),
  gynecology: require('../../../assets/images/specialties/gynecology.jpg'),
  ent: require('../../../assets/images/specialties/ent.jpg'),
  ophthalmology: require('../../../assets/images/specialties/ophthalmology.jpg'),
  psychiatry: require('../../../assets/images/specialties/psychiatry.jpg'),
  dentistry: require('../../../assets/images/specialties/dentistry.jpg'),
  ayurveda: require('../../../assets/images/specialties/ayurveda.jpg'),
  neurology: require('../../../assets/images/specialties/neurology.jpg'),
};

const fallbackImage = specialtyImages['general-medicine'];

/** Preferred homepage order aligned with reference layout. */
const homepageOrder = [
  'general-medicine',
  'gynecology',
  'dermatology',
  'pediatrics',
  'dentistry',
  'ophthalmology',
  'ent',
  'psychiatry',
  'cardiology',
  'orthopedics',
  'neurology',
  'ayurveda',
] as const;

const generalCareCodes = new Set([
  'general-medicine',
  'gynecology',
  'dermatology',
  'pediatrics',
  'dentistry',
  'ophthalmology',
  'ent',
  'psychiatry',
]);

/** Friendlier card labels for known codes; falls back to API name. */
const displayNames: Record<string, { en: string; hi: string }> = {
  'general-medicine': { en: 'General Physician', hi: 'जनरल फिज़िशियन' },
  gynecology: { en: "Women's Health", hi: 'महिला स्वास्थ्य' },
  dermatology: { en: 'Dermatology', hi: 'डर्मेटोलॉजी' },
  pediatrics: { en: 'Pediatrics', hi: 'पीडियाट्रिक्स' },
  dentistry: { en: 'Dentist', hi: 'दंत चिकित्सक' },
  ophthalmology: { en: 'Eye Specialist', hi: 'नेत्र विशेषज्ञ' },
  ent: { en: 'ENT', hi: 'कान-नाक-गला' },
  psychiatry: { en: 'Mental Wellness', hi: 'मानसिक स्वास्थ्य' },
  cardiology: { en: 'Cardiology', hi: 'कार्डियोलॉजी' },
  orthopedics: { en: 'Orthopedics', hi: 'ऑर्थोपेडिक्स' },
  neurology: { en: 'Neurology', hi: 'न्यूरोलॉजी' },
  ayurveda: { en: 'Ayurveda', hi: 'आयुर्वेद' },
};

export type SpecialtyCareGroup = 'general' | 'advanced';

export type SpecialtyCardModel = {
  id: string;
  code: string;
  name: string;
  label: string;
  image: ImageSource;
  group: SpecialtyCareGroup;
};

export function specialtyImageForCode(code: string): ImageSource {
  return specialtyImages[code.toLowerCase()] ?? fallbackImage;
}

export function buildSpecialtyCards(
  rows: Specialization[],
  language: 'en' | 'hi',
): SpecialtyCardModel[] {
  const byCode = new Map(rows.map((row) => [row.code.toLowerCase(), row]));
  const ordered: Specialization[] = [];

  for (const code of homepageOrder) {
    const hit = byCode.get(code);
    if (hit) {
      ordered.push(hit);
      byCode.delete(code);
    }
  }

  // Append any additional backend specialties without inventing categories.
  for (const leftover of byCode.values()) {
    ordered.push(leftover);
  }

  return ordered.map((item) => {
    const code = item.code.toLowerCase();
    const localized = displayNames[code];
    return {
      id: item.id,
      code,
      name: item.name,
      label: localized ? localized[language] : item.name,
      image: specialtyImageForCode(code),
      group: generalCareCodes.has(code) ? 'general' : 'advanced',
    };
  });
}
