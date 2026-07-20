import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { isAxiosError } from 'axios'
import { useAreas } from '../areas/useAreas'
import { useLabels } from '../labels/useLabels'
import { useCreateDevice, useDeleteDevice, useDevices, useUpdateDevice } from '../devices/useDevices'
import type { Device, DeviceType } from '../devices/types'
import type { ApiError } from '../auth/types'

const DEVICE_TYPES: DeviceType[] = ['Light', 'Socket', 'Thermostat', 'Camera', 'Tv']

function errorCodeToMessage(t: (key: string) => string, err: unknown): string {
  if (isAxiosError<ApiError>(err) && err.response?.data?.code) {
    return t(`devices.errors.${err.response.data.code}`)
  }
  return t('devices.errors.GENERIC')
}

interface EditState {
  id: string
  name: string
  areaId: string
  labelIds: string[]
}

export default function Devices() {
  const { t } = useTranslation()
  const { data: devices } = useDevices()
  const { data: areas } = useAreas()
  const { data: labels } = useLabels()
  const createDevice = useCreateDevice()
  const updateDevice = useUpdateDevice()
  const deleteDevice = useDeleteDevice()

  const [newName, setNewName] = useState('')
  const [newType, setNewType] = useState<DeviceType>('Light')
  const [newAreaId, setNewAreaId] = useState('')
  const [editing, setEditing] = useState<EditState | null>(null)
  const [error, setError] = useState<string | null>(null)

  const onCreate = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    setError(null)
    try {
      await createDevice.mutateAsync({ name: newName, type: newType, areaId: newAreaId || null })
      setNewName('')
      setNewAreaId('')
    } catch (err) {
      setError(errorCodeToMessage(t, err))
    }
  }

  const startEdit = (device: Device) => {
    setEditing({
      id: device.id,
      name: device.name,
      areaId: device.areaId ?? '',
      labelIds: device.labels.map((l) => l.id),
    })
  }

  const toggleLabel = (labelId: string) => {
    if (!editing) return
    setEditing({
      ...editing,
      labelIds: editing.labelIds.includes(labelId)
        ? editing.labelIds.filter((id) => id !== labelId)
        : [...editing.labelIds, labelId],
    })
  }

  const onSaveEdit = async () => {
    if (!editing) return
    const device = devices?.find((d) => d.id === editing.id)
    if (!device) return
    setError(null)
    try {
      await updateDevice.mutateAsync({
        id: editing.id,
        body: {
          name: editing.name,
          areaId: editing.areaId || null,
          labelIds: editing.labelIds,
          attributes: device.attributes,
        },
      })
      setEditing(null)
    } catch (err) {
      setError(errorCodeToMessage(t, err))
    }
  }

  const onDelete = async (id: string) => {
    if (!window.confirm(t('devices.deleteConfirm'))) return
    setError(null)
    try {
      await deleteDevice.mutateAsync(id)
    } catch (err) {
      setError(errorCodeToMessage(t, err))
    }
  }

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100">
      <header className="border-b border-slate-800 px-6 py-3 flex items-center justify-between">
        <Link to="/" className="text-xl font-bold">
          {t('app.name')}
        </Link>
      </header>

      <main className="p-6 max-w-2xl mx-auto flex flex-col gap-6">
        <h1 className="text-2xl font-semibold">{t('devices.title')}</h1>

        {error && <div className="rounded bg-red-500/15 text-red-400 px-3 py-2 text-sm">{error}</div>}

        <form onSubmit={onCreate} className="rounded-xl bg-slate-900 border border-slate-800 p-4 flex flex-wrap gap-2 items-end">
          <label className="flex flex-col gap-1 text-sm">
            <span className="text-slate-300">{t('devices.nameLabel')}</span>
            <input
              type="text"
              value={newName}
              onChange={(e) => setNewName(e.target.value)}
              required
              className="rounded bg-slate-800 border border-slate-700 px-3 py-2 outline-none focus:border-indigo-500"
            />
          </label>
          <label className="flex flex-col gap-1 text-sm">
            <span className="text-slate-300">{t('devices.typeLabel')}</span>
            <select
              value={newType}
              onChange={(e) => setNewType(e.target.value as DeviceType)}
              className="rounded bg-slate-800 border border-slate-700 px-3 py-2 outline-none focus:border-indigo-500"
            >
              {DEVICE_TYPES.map((type) => (
                <option key={type} value={type}>
                  {t(`devices.type.${type}`)}
                </option>
              ))}
            </select>
          </label>
          <label className="flex flex-col gap-1 text-sm">
            <span className="text-slate-300">{t('devices.areaLabel')}</span>
            <select
              value={newAreaId}
              onChange={(e) => setNewAreaId(e.target.value)}
              className="rounded bg-slate-800 border border-slate-700 px-3 py-2 outline-none focus:border-indigo-500"
            >
              <option value="">{t('devices.noArea')}</option>
              {areas?.map((area) => (
                <option key={area.id} value={area.id}>
                  {area.name}
                </option>
              ))}
            </select>
          </label>
          <button
            type="submit"
            disabled={createDevice.isPending}
            className="rounded bg-indigo-500 hover:bg-indigo-400 disabled:opacity-50 px-4 py-2 text-sm font-medium transition-colors"
          >
            {t('devices.addButton')}
          </button>
        </form>

        <div className="rounded-xl bg-slate-900 border border-slate-800 p-4">
          <ul className="flex flex-col gap-3">
            {devices?.map((device) => (
              <li key={device.id} className="rounded bg-slate-800 p-3">
                {editing?.id === device.id ? (
                  <div className="flex flex-col gap-2">
                    <input
                      type="text"
                      value={editing.name}
                      onChange={(e) => setEditing({ ...editing, name: e.target.value })}
                      className="rounded bg-slate-900 border border-slate-700 px-2 py-1 outline-none focus:border-indigo-500"
                    />
                    <select
                      value={editing.areaId}
                      onChange={(e) => setEditing({ ...editing, areaId: e.target.value })}
                      className="rounded bg-slate-900 border border-slate-700 px-2 py-1 outline-none focus:border-indigo-500"
                    >
                      <option value="">{t('devices.noArea')}</option>
                      {areas?.map((area) => (
                        <option key={area.id} value={area.id}>
                          {area.name}
                        </option>
                      ))}
                    </select>
                    <div className="flex flex-wrap gap-2 text-sm">
                      {labels?.map((label) => (
                        <label key={label.id} className="flex items-center gap-1">
                          <input
                            type="checkbox"
                            checked={editing.labelIds.includes(label.id)}
                            onChange={() => toggleLabel(label.id)}
                          />
                          {label.name}
                        </label>
                      ))}
                    </div>
                    <div className="flex gap-2">
                      <button
                        type="button"
                        onClick={onSaveEdit}
                        className="rounded bg-indigo-500 hover:bg-indigo-400 px-3 py-1 text-sm transition-colors"
                      >
                        {t('devices.save')}
                      </button>
                      <button
                        type="button"
                        onClick={() => setEditing(null)}
                        className="rounded bg-slate-700 hover:bg-slate-600 px-3 py-1 text-sm transition-colors"
                      >
                        {t('devices.cancel')}
                      </button>
                    </div>
                  </div>
                ) : (
                  <div className="flex items-center justify-between gap-2">
                    <div className="flex flex-col gap-1">
                      <div className="flex items-center gap-2">
                        <span className="font-medium">{device.name}</span>
                        <span className="rounded-full bg-indigo-500/15 text-indigo-400 px-2 py-0.5 text-xs">
                          {t(`devices.type.${device.type}`)}
                        </span>
                      </div>
                      <div className="text-sm text-slate-400">
                        {device.areaName ?? t('devices.noArea')}
                        {device.labels.length > 0 && (
                          <>
                            {' · '}
                            {device.labels.map((l) => l.name).join(', ')}
                          </>
                        )}
                      </div>
                    </div>
                    <div className="flex gap-2">
                      <button
                        type="button"
                        onClick={() => startEdit(device)}
                        className="rounded bg-slate-700 hover:bg-slate-600 px-3 py-1 text-sm transition-colors"
                      >
                        {t('devices.edit')}
                      </button>
                      <button
                        type="button"
                        onClick={() => onDelete(device.id)}
                        className="rounded bg-red-500/15 hover:bg-red-500/25 text-red-400 px-3 py-1 text-sm transition-colors"
                      >
                        {t('devices.delete')}
                      </button>
                    </div>
                  </div>
                )}
              </li>
            ))}
          </ul>
        </div>
      </main>
    </div>
  )
}
