import type { DeviceType } from '../devices/types'

// Frontend mirror of the backend's DeviceCapabilityRegistry state outputs.
export const STATES_BY_TYPE: Record<DeviceType, string[]> = {
  Light: ['on', 'off'],
  Socket: ['on', 'off'],
  Thermostat: ['idle', 'heating', 'cooling', 'auto'],
  // Camera has no supported actions (always UNKNOWN_ACTION), so its state never changes
  // post-creation — excluded from the trigger-device picker for the same reason Scenes'
  // step editor excludes it from the actionable-device list.
  Camera: [],
  Tv: ['on', 'off'],
}
