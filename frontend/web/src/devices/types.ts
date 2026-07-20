import type { Label } from '../labels/types'

export type DeviceType = 'Light' | 'Socket' | 'Thermostat' | 'Camera' | 'Tv'

export interface LightAttributes {
  isOn: boolean
  brightness: number | null
  colorHex: string | null
}

export interface SocketAttributes {
  isOn: boolean
}

export type ThermostatMode = 'Off' | 'Heat' | 'Cool' | 'Auto'

export interface ThermostatAttributes {
  targetTemperatureC: number
  currentTemperatureC: number | null
  mode: ThermostatMode
}

export interface CameraAttributes {
  isOnline: boolean
}

export interface TvAttributes {
  isOn: boolean
  volume: number
  source: string | null
}

export interface DeviceAttributes {
  light: LightAttributes | null
  socket: SocketAttributes | null
  thermostat: ThermostatAttributes | null
  camera: CameraAttributes | null
  tv: TvAttributes | null
}

export interface Device {
  id: string
  name: string
  type: DeviceType
  areaId: string | null
  areaName: string | null
  state: string
  attributes: DeviceAttributes
  labels: Label[]
  createdAtUtc: string
}
