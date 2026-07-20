import { api } from './client'
import type { Label } from '../labels/types'

export const fetchLabels = () => api.get<Label[]>('/api/labels').then((r) => r.data)

export const createLabel = (name: string) =>
  api.post<Label>('/api/labels', { name }).then((r) => r.data)

export const updateLabel = (id: string, name: string) =>
  api.put<Label>(`/api/labels/${id}`, { name }).then((r) => r.data)

export const deleteLabel = (id: string) => api.delete(`/api/labels/${id}`)
