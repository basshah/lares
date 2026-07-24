import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  createAutomation,
  deleteAutomation,
  fetchAutomations,
  runAutomation,
  setAutomationEnabled,
  updateAutomation,
  type AutomationBody,
} from '../api/automations'
import { devicesQueryKey } from '../devices/useDevices'

export const automationsQueryKey = ['automations'] as const

export function useAutomations() {
  return useQuery({ queryKey: automationsQueryKey, queryFn: fetchAutomations })
}

function useInvalidateAutomationsMutation<TVariables>(mutationFn: (variables: TVariables) => Promise<unknown>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: automationsQueryKey }),
  })
}

export const useCreateAutomation = () =>
  useInvalidateAutomationsMutation((body: AutomationBody) => createAutomation(body))

export const useUpdateAutomation = () =>
  useInvalidateAutomationsMutation(({ id, body }: { id: string; body: AutomationBody }) => updateAutomation(id, body))

export const useSetAutomationEnabled = () =>
  useInvalidateAutomationsMutation(({ id, isEnabled }: { id: string; isEnabled: boolean }) => setAutomationEnabled(id, isEnabled))

export const useDeleteAutomation = () => useInvalidateAutomationsMutation((id: string) => deleteAutomation(id))

export function useRunAutomation() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => runAutomation(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: automationsQueryKey })
      queryClient.invalidateQueries({ queryKey: devicesQueryKey })
    },
  })
}
