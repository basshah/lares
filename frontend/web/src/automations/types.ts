export type AutomationTriggerType = 'Time' | 'DeviceState'
export type AutomationDayOfWeek = 'Sunday' | 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday'

export interface AutomationStep {
  id: string
  deviceId: string
  deviceName: string
  order: number
  action: string
  params: Record<string, unknown> | null
}

export interface Automation {
  id: string
  name: string
  isEnabled: boolean
  triggerType: AutomationTriggerType
  triggerTimeOfDay: string | null // "HH:mm:ss"
  triggerDaysOfWeek: AutomationDayOfWeek[] | null // null = every day
  triggerDeviceId: string | null
  triggerDeviceName: string | null
  triggerState: string | null
  steps: AutomationStep[]
  lastTriggeredAtUtc: string | null
  createdAtUtc: string
}
