import { api } from './client'
import type { Device, DeviceAttributes, DeviceType } from '../devices/types'

export interface CreateDeviceBody {
  name: string
  type: DeviceType
  areaId: string | null
}

export interface UpdateDeviceBody {
  name: string
  areaId: string | null
  labelIds: string[]
  attributes: DeviceAttributes
}

export const fetchDevices = (filters?: { areaId?: string; labelId?: string }) =>
  api.get<Device[]>('/api/devices', { params: filters }).then((r) => r.data)

export const createDevice = (body: CreateDeviceBody) =>
  api.post<Device>('/api/devices', body).then((r) => r.data)

export const updateDevice = (id: string, body: UpdateDeviceBody) =>
  api.put<Device>(`/api/devices/${id}`, body).then((r) => r.data)

export const deleteDevice = (id: string) => api.delete(`/api/devices/${id}`)
