import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { createHome, fetchMyHome, joinHome, leaveHome, regenerateInviteCode } from '../api/home'

export const homeQueryKey = ['home', 'me'] as const

export function useMyHome() {
  return useQuery({ queryKey: homeQueryKey, queryFn: fetchMyHome })
}

function useInvalidateHomeMutation<TVariables = void>(mutationFn: (variables: TVariables) => Promise<unknown>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: homeQueryKey }),
  })
}

export const useCreateHome = () => useInvalidateHomeMutation((name: string) => createHome(name))
export const useJoinHome = () => useInvalidateHomeMutation((inviteCode: string) => joinHome(inviteCode))
export const useRegenerateInvite = () => useInvalidateHomeMutation(() => regenerateInviteCode())
export const useLeaveHome = () => useInvalidateHomeMutation(() => leaveHome())
