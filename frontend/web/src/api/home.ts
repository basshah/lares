import { isAxiosError } from 'axios'
import { api } from './client'
import type { HomeSummary } from '../home/types'

export async function fetchMyHome(): Promise<HomeSummary | null> {
  try {
    const { data } = await api.get<HomeSummary>('/api/homes/me')
    return data
  } catch (err) {
    if (isAxiosError(err) && err.response?.status === 404) return null
    throw err
  }
}

export const createHome = (name: string) =>
  api.post<HomeSummary>('/api/homes/create', { name }).then((r) => r.data)

export const joinHome = (inviteCode: string) =>
  api.post<HomeSummary>('/api/homes/join', { inviteCode }).then((r) => r.data)

export const regenerateInviteCode = () =>
  api.post<{ inviteCode: string }>('/api/homes/regenerate-invite').then((r) => r.data)

export const leaveHome = () => api.post('/api/homes/leave')
