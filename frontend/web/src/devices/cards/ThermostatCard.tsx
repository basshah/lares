import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useDeviceAction } from '../useDevices'
import type { Device, ThermostatMode } from '../types'

const MODES: ThermostatMode[] = ['Off', 'Heat', 'Cool', 'Auto']

export default function ThermostatCard({ device }: { device: Device }) {
  const { t } = useTranslation()
  const action = useDeviceAction()
  const thermostat = device.attributes.thermostat!
  const [target, setTarget] = useState(thermostat.targetTemperatureC)

  useEffect(() => {
    setTarget(thermostat.targetTemperatureC)
  }, [thermostat.targetTemperatureC])

  const commitTarget = () => action.mutate({ id: device.id, action: 'setTargetTemperature', params: { value: target } })

  const onModeChange = (mode: string) => action.mutate({ id: device.id, action: 'setMode', params: { mode } })

  return (
    <div className="rounded-xl bg-slate-900 border border-slate-800 p-4 flex flex-col gap-3">
      <div className="flex flex-col">
        <span className="font-medium">{device.name}</span>
        <span className="text-sm text-slate-400">{device.areaName ?? t('devices.noArea')}</span>
      </div>

      <div className="text-sm text-slate-400">
        {t('dashboard.currentTemperature')}:{' '}
        {thermostat.currentTemperatureC != null ? `${thermostat.currentTemperatureC}°C` : '—'}
      </div>

      <label className="flex flex-col gap-1 text-sm text-slate-400">
        {t('dashboard.targetTemperature')}
        <input
          type="number"
          value={target}
          onChange={(e) => setTarget(Number(e.target.value))}
          onBlur={commitTarget}
          onKeyDown={(e) => e.key === 'Enter' && commitTarget()}
          className="rounded bg-slate-800 border border-slate-700 px-3 py-1 text-slate-100 outline-none focus:border-indigo-500"
        />
      </label>

      <label className="flex flex-col gap-1 text-sm text-slate-400">
        {t('dashboard.mode')}
        <select
          value={thermostat.mode}
          onChange={(e) => onModeChange(e.target.value)}
          className="rounded bg-slate-800 border border-slate-700 px-3 py-1 text-slate-100 outline-none focus:border-indigo-500"
        >
          {MODES.map((mode) => (
            <option key={mode} value={mode}>
              {t(`devices.mode.${mode}`)}
            </option>
          ))}
        </select>
      </label>
    </div>
  )
}
