import { api } from './client'
import type { Scene } from '../scenes/types'

export interface SceneStepBody {
  deviceId: string
  action: string
  params?: Record<string, unknown> | null
}

export interface SceneBody {
  name: string
  steps: SceneStepBody[]
}

export interface SceneStepResult {
  deviceId: string
  deviceName: string
  action: string
  success: boolean
  errorCode: string | null
}

export interface SceneExecuteResult {
  sceneId: string
  sceneName: string
  results: SceneStepResult[]
}

export const fetchScenes = () => api.get<Scene[]>('/api/scenes').then((r) => r.data)

export const createScene = (body: SceneBody) => api.post<Scene>('/api/scenes', body).then((r) => r.data)

export const updateScene = (id: string, body: SceneBody) =>
  api.put<Scene>(`/api/scenes/${id}`, body).then((r) => r.data)

export const deleteScene = (id: string) => api.delete(`/api/scenes/${id}`)

export const executeScene = (id: string) =>
  api.post<SceneExecuteResult>(`/api/scenes/${id}/execute`).then((r) => r.data)
