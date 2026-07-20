import { useTranslation } from 'react-i18next'
import { useDeviceAction } from '../useDevices'
import type { Device } from '../types'

export default function SocketCard({ device }: { device: Device }) {
  const { t } = useTranslation()
  const action = useDeviceAction()
  const socket = device.attributes.socket!

  const toggle = () => action.mutate({ id: device.id, action: socket.isOn ? 'turnOff' : 'turnOn' })

  return (
    <div className="rounded-xl bg-slate-900 border border-slate-800 p-4 flex items-center justify-between">
      <div className="flex flex-col">
        <span className="font-medium">{device.name}</span>
        <span className="text-sm text-slate-400">{device.areaName ?? t('devices.noArea')}</span>
      </div>
      <button
        type="button"
        onClick={toggle}
        className={`rounded-full px-4 py-1 text-sm transition-colors ${
          socket.isOn ? 'bg-indigo-500 hover:bg-indigo-400' : 'bg-slate-800 hover:bg-slate-700'
        }`}
      >
        {socket.isOn ? t('dashboard.turnOff') : t('dashboard.turnOn')}
      </button>
    </div>
  )
}
