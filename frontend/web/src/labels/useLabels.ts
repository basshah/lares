import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { createLabel, deleteLabel, fetchLabels, updateLabel } from '../api/labels'
import { devicesQueryKey } from '../devices/useDevices'

export const labelsQueryKey = ['labels'] as const

export function useLabels() {
  return useQuery({ queryKey: labelsQueryKey, queryFn: fetchLabels })
}

function useInvalidateLabelsMutation<TVariables>(mutationFn: (variables: TVariables) => Promise<unknown>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: labelsQueryKey })
      queryClient.invalidateQueries({ queryKey: devicesQueryKey })
    },
  })
}

export const useCreateLabel = () => useInvalidateLabelsMutation((name: string) => createLabel(name))

export const useUpdateLabel = () =>
  useInvalidateLabelsMutation(({ id, name }: { id: string; name: string }) => updateLabel(id, name))

export const useDeleteLabel = () => useInvalidateLabelsMutation((id: string) => deleteLabel(id))
