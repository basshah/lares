import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useDeviceAction } from '../useDevices'
import type { Device } from '../types'

export default function LightCard({ device }: { device: Device }) {
  const { t } = useTranslation()
  const action = useDeviceAction()
  const light = device.attributes.light!
  const [brightness, setBrightness] = useState(light.brightness ?? 0)

  useEffect(() => {
    setBrightness(light.brightness ?? 0)
  }, [light.brightness])

  const toggle = () => action.mutate({ id: device.id, action: light.isOn ? 'turnOff' : 'turnOn' })

  const commitBrightness = (value: number) =>
    action.mutate({ id: device.id, action: 'setBrightness', params: { value } })

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
            light.isOn ? 'bg-indigo-500 hover:bg-indigo-400' : 'bg-slate-800 hover:bg-slate-700'
          }`}
        >
          {light.isOn ? t('dashboard.turnOff') : t('dashboard.turnOn')}
        </button>
      </div>
      {light.isOn && (
        <label className="flex flex-col gap-1 text-sm text-slate-400">
          {t('dashboard.brightness')}
          <input
            type="range"
            min={0}
            max={100}
            value={brightness}
            onChange={(e) => setBrightness(Number(e.target.value))}
            onPointerUp={(e) => commitBrightness(Number((e.target as HTMLInputElement).value))}
          />
        </label>
      )}
    </div>
  )
}
