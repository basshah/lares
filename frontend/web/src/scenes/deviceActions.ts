import type { DeviceType } from '../devices/types'

export interface ActionParamSpec {
  key: string
  kind: 'percent' | 'number' | 'enum'
  enumOptions?: string[]
}

export interface ActionSpec {
  action: string
  params: ActionParamSpec[]
}

// Frontend mirror of the backend's DeviceCapabilityRegistry action table.
export const ACTIONS_BY_TYPE: Record<DeviceType, ActionSpec[]> = {
  Light: [
    { action: 'turnOn', params: [] },
    { action: 'turnOff', params: [] },
    { action: 'setBrightness', params: [{ key: 'value', kind: 'percent' }] },
  ],
  Socket: [
    { action: 'turnOn', params: [] },
    { action: 'turnOff', params: [] },
  ],
  Thermostat: [
    { action: 'setTargetTemperature', params: [{ key: 'value', kind: 'number' }] },
    { action: 'setMode', params: [{ key: 'mode', kind: 'enum', enumOptions: ['Off', 'Heat', 'Cool', 'Auto'] }] },
  ],
  Camera: [],
  Tv: [
    { action: 'turnOn', params: [] },
    { action: 'turnOff', params: [] },
    { action: 'setVolume', params: [{ key: 'value', kind: 'percent' }] },
  ],
}
