import { useTranslation } from 'react-i18next'
import type { Device } from '../types'

export default function CameraCard({ device }: { device: Device }) {
  const { t } = useTranslation()
  const camera = device.attributes.camera!

  return (
    <div className="rounded-xl bg-slate-900 border border-slate-800 p-4 flex items-center justify-between">
      <div className="flex flex-col">
        <span className="font-medium">{device.name}</span>
        <span className="text-sm text-slate-400">{device.areaName ?? t('devices.noArea')}</span>
      </div>
      <span
        className={`rounded-full px-3 py-0.5 text-xs ${
          camera.isOnline ? 'bg-emerald-500/15 text-emerald-400' : 'bg-slate-500/15 text-slate-400'
        }`}
      >
        {camera.isOnline ? t('dashboard.state.online') : t('dashboard.state.offline')}
      </span>
    </div>
  )
}
