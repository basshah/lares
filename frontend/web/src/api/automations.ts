import { api } from './client'
import type { Automation, AutomationDayOfWeek, AutomationTriggerType } from '../automations/types'
import type { StepBody } from '../steps/StepEditor'

export interface AutomationBody {
  name: string
  isEnabled: boolean
  triggerType: AutomationTriggerType
  triggerTimeOfDay: string | null // "HH:mm:ss"
  triggerDaysOfWeek: AutomationDayOfWeek[] | null
  triggerDeviceId: string | null
  triggerState: string | null
  steps: StepBody[]
}

export interface AutomationStepResult {
  deviceId: string
  deviceName: string
  action: string
  success: boolean
  errorCode: string | null
}

export interface AutomationExecuteResult {
  automationId: string
  automationName: string
  results: AutomationStepResult[]
}

export const fetchAutomations = () => api.get<Automation[]>('/api/automations').then((r) => r.data)

export const createAutomation = (body: AutomationBody) =>
  api.post<Automation>('/api/automations', body).then((r) => r.data)

export const updateAutomation = (id: string, body: AutomationBody) =>
  api.put<Automation>(`/api/automations/${id}`, body).then((r) => r.data)

export const setAutomationEnabled = (id: string, isEnabled: boolean) =>
  api.patch<Automation>(`/api/automations/${id}/enabled`, { isEnabled }).then((r) => r.data)

export const deleteAutomation = (id: string) => api.delete(`/api/automations/${id}`)

export const runAutomation = (id: string) =>
  api.post<AutomationExecuteResult>(`/api/automations/${id}/run`).then((r) => r.data)
