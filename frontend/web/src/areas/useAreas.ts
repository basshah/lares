import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { createArea, deleteArea, fetchAreas, updateArea } from '../api/areas'
import { devicesQueryKey } from '../devices/useDevices'

export const areasQueryKey = ['areas'] as const

export function useAreas() {
  return useQuery({ queryKey: areasQueryKey, queryFn: fetchAreas })
}

function useInvalidateAreasMutation<TVariables>(mutationFn: (variables: TVariables) => Promise<unknown>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: areasQueryKey })
      queryClient.invalidateQueries({ queryKey: devicesQueryKey })
    },
  })
}

export const useCreateArea = () => useInvalidateAreasMutation((name: string) => createArea(name))

export const useUpdateArea = () =>
  useInvalidateAreasMutation(({ id, name }: { id: string; name: string }) => updateArea(id, name))

export const useDeleteArea = () => useInvalidateAreasMutation((id: string) => deleteArea(id))
