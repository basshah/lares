export interface SceneStep {
  id: string
  deviceId: string
  deviceName: string
  order: number
  action: string
  params: Record<string, unknown> | null
}

export interface Scene {
  id: string
  name: string
  steps: SceneStep[]
  createdAtUtc: string
}
