export type ChatRole = 'User' | 'Assistant'

export interface ChatMessage {
  id: string
  role: ChatRole
  content: string
  createdAtUtc: string
}
