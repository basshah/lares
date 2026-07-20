import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  createDevice,
  deleteDevice,
  fetchDevices,
  performDeviceAction,
  updateDevice,
  type CreateDeviceBody,
  type UpdateDeviceBody,
} from '../api/devices'

export const devicesQueryKey = ['devices'] as const

export function useDevices(filters?: { areaId?: string; labelId?: string }) {
  return useQuery({ queryKey: [...devicesQueryKey, filters], queryFn: () => fetchDevices(filters) })
}

function useInvalidateDevicesMutation<TVariables>(mutationFn: (variables: TVariables) => Promise<unknown>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: devicesQueryKey }),
  })
}

export const useCreateDevice = () => useInvalidateDevicesMutation((body: CreateDeviceBody) => createDevice(body))

export const useUpdateDevice = () =>
  useInvalidateDevicesMutation(({ id, body }: { id: string; body: UpdateDeviceBody }) => updateDevice(id, body))

export const useDeleteDevice = () => useInvalidateDevicesMutation((id: string) => deleteDevice(id))

export const useDeviceAction = () =>
  useInvalidateDevicesMutation(
    ({ id, action, params }: { id: string; action: string; params?: Record<string, unknown> }) =>
      performDeviceAction(id, action, params),
  )
