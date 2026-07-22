import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { fetchChatHistory, sendChatMessage } from '../api/chat'

export const chatMessagesQueryKey = ['chat', 'messages'] as const

export function useChatHistory() {
  return useQuery({ queryKey: chatMessagesQueryKey, queryFn: fetchChatHistory })
}

export function useSendChatMessage() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (message: string) => sendChatMessage(message),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: chatMessagesQueryKey }),
  })
}
