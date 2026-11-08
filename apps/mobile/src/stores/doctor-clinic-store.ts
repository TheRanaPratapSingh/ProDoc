import AsyncStorage from '@react-native-async-storage/async-storage';
import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';

type DoctorClinicState = {
  selectedClinicId: string | null;
  setSelectedClinicId: (clinicId: string | null) => void;
};

/** Selected clinic for doctor portal surfaces — null means All Clinics. */
export const useDoctorClinicStore = create<DoctorClinicState>()(
  persist(
    (set) => ({
      selectedClinicId: null,
      setSelectedClinicId: (clinicId) => set({ selectedClinicId: clinicId }),
    }),
    {
      name: 'prodoc.doctor.clinic',
      storage: createJSONStorage(() => AsyncStorage),
    },
  ),
);
