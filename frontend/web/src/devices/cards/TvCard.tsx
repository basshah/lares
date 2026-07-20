import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useDeviceAction } from '../useDevices'
import type { Device } from '../types'

export default function TvCard({ device }: { device: Device }) {
  const { t } = useTranslation()
  const action = useDeviceAction()
  const tv = device.attributes.tv!
  const [volume, setVolume] = useState(tv.volume)

  useEffect(() => {
    setVolume(tv.volume)
  }, [tv.volume])

  const toggle = () => action.mutate({ id: device.id, action: tv.isOn ? 'turnOff' : 'turnOn' })

  const commitVolume = (value: number) => action.mutate({ id: device.id, action: 'setVolume', params: { value } })

  return (
    <div className="rounded-xl bg-slate-900 border border-slate-800 p-4 flex flex-col gap-3">
      <div className="flex items-center justify-between">
        <div className="flex flex-col">
          <span className="font-medium">{device.name}</span>
          <span className="text-sm text-slate-400">{device.areaName ?? t('devices.noArea')}</span>
        </div>
        <button
          type="button"
          onClick={toggle}
          className={`rounded-full px-4 py-1 text-sm transition-colors ${
            tv.isOn ? 'bg-indigo-500 hover:bg-indigo-400' : 'bg-slate-800 hover:bg-slate-700'
          }`}
        >
          {tv.isOn ? t('dashboard.turnOff') : t('dashboard.turnOn')}
        </button>
      </div>
      {tv.isOn && (
        <label className="flex flex-col gap-1 text-sm text-slate-400">
          {t('dashboard.volume')}
          <input
            type="range"
            min={0}
            max={100}
            value={volume}
            onChange={(e) => setVolume(Number(e.target.value))}
            onPointerUp={(e) => commitVolume(Number((e.target as HTMLInputElement).value))}
          />
        </label>
      )}
      <div className="text-sm text-slate-400">
        {t('dashboard.source')}: {tv.source ?? '—'}
      </div>
    </div>
  )
}
