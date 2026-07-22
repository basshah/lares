import { api } from './client'
import type { ChatMessage } from '../chat/types'

export const fetchChatHistory = () => api.get<ChatMessage[]>('/api/chat/messages').then((r) => r.data)

export const sendChatMessage = (message: string) =>
  api.post<ChatMessage>('/api/chat/messages', { message }).then((r) => r.data)
