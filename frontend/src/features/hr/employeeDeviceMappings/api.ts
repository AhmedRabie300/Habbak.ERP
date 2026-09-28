import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

/** Docs/Implementation/Phase-3B-Research.md §Phase 3B — HR_EMPLOYEE_DEVICE_MAPPINGS. */

export interface EmployeeDeviceMapping {
  id: number;
  employeeId: number;
  employeeCode: string;
  employeeNameAr: string;
  attendanceDeviceId: number;
  attendanceDeviceCode: string;
  deviceUserId: string;
}

const BASE = '/hr/employee-device-mappings';

export function useEmployeeDeviceMappings(filter: { employeeId?: number; attendanceDeviceId?: number } = {}) {
  return useQuery({
    queryKey: ['hr-employee-device-mappings', filter.employeeId, filter.attendanceDeviceId],
    queryFn: async () => (await api.get<EmployeeDeviceMapping[]>(BASE, { params: filter })).data
  });
}

export function useCreateEmployeeDeviceMapping() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: { employeeId: number; attendanceDeviceId: number; deviceUserId: string }) =>
      (await api.post<{ id: number }>(BASE, data)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-employee-device-mappings'] })
  });
}

export function useDeleteEmployeeDeviceMapping() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => {
      await api.delete(`${BASE}/${id}`);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-employee-device-mappings'] })
  });
}
