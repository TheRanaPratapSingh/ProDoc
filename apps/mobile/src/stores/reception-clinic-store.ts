import AsyncStorage from '@react-native-async-storage/async-storage';
import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';

type ReceptionClinicState = {
  selectedClinicId: string | null;
  selectedBranchId: string | null;
  setSelectedClinicId: (clinicId: string | null) => void;
  setSelectedBranchId: (branchId: string | null) => void;
  setSelection: (clinicId: string | null, branchId: string | null) => void;
};

/** Selected clinic/branch for receptionist portal — filters all desk data. */
export const useReceptionClinicStore = create<ReceptionClinicState>()(
  persist(
    (set) => ({
      selectedClinicId: null,
      selectedBranchId: null,
      setSelectedClinicId: (clinicId) => set({ selectedClinicId: clinicId, selectedBranchId: null }),
      setSelectedBranchId: (branchId) => set({ selectedBranchId: branchId }),
      setSelection: (clinicId, branchId) => set({ selectedClinicId: clinicId, selectedBranchId: branchId }),
    }),
    {
      name: 'prodoc.reception.clinic',
      storage: createJSONStorage(() => AsyncStorage),
    },
  ),
);
