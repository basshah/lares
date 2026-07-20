import { api } from './client'
import type { Area } from '../areas/types'

export const fetchAreas = () => api.get<Area[]>('/api/areas').then((r) => r.data)

export const createArea = (name: string) =>
  api.post<Area>('/api/areas', { name }).then((r) => r.data)

export const updateArea = (id: string, name: string) =>
  api.put<Area>(`/api/areas/${id}`, { name }).then((r) => r.data)

export const deleteArea = (id: string) => api.delete(`/api/areas/${id}`)
