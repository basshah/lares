import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { createScene, deleteScene, executeScene, fetchScenes, updateScene, type SceneBody } from '../api/scenes'
import { devicesQueryKey } from '../devices/useDevices'

export const scenesQueryKey = ['scenes'] as const

export function useScenes() {
  return useQuery({ queryKey: scenesQueryKey, queryFn: fetchScenes })
}

function useInvalidateScenesMutation<TVariables>(mutationFn: (variables: TVariables) => Promise<unknown>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: scenesQueryKey }),
  })
}

export const useCreateScene = () => useInvalidateScenesMutation((body: SceneBody) => createScene(body))

export const useUpdateScene = () =>
  useInvalidateScenesMutation(({ id, body }: { id: string; body: SceneBody }) => updateScene(id, body))

export const useDeleteScene = () => useInvalidateScenesMutation((id: string) => deleteScene(id))

export function useRunScene() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => executeScene(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: scenesQueryKey })
      queryClient.invalidateQueries({ queryKey: devicesQueryKey })
    },
  })
}
