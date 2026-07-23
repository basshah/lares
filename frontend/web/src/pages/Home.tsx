import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { useAreas } from '../areas/useAreas'
import { useLabels } from '../labels/useLabels'
import { useDevices } from '../devices/useDevices'
import { badgeIcon, badgeValue } from '../devices/badges'
import DeviceCard from '../devices/cards/DeviceCard'
import type { Device } from '../devices/types'
import ChatPanel from '../chat/components/ChatPanel'
import { useRunScene, useScenes } from '../scenes/useScenes'

const NO_AREA = '__none__'

export default function Home() {
  const { t, i18n } = useTranslation()
  const { user, logout } = useAuth()

  const { data: devices } = useDevices()
  const { data: areas } = useAreas()
  const { data: labels } = useLabels()
  const { data: scenes } = useScenes()
  const runScene = useRunScene()

  const [selectedLabelId, setSelectedLabelId] = useState('')

  const filteredDevices = useMemo(
    () =>
      devices?.filter((d) => !selectedLabelId || d.labels.some((l) => l.id === selectedLabelId)) ?? [],
    [devices, selectedLabelId],
  )

  const groups = useMemo(() => {
    const byArea = new Map<string, Device[]>()
    for (const device of filteredDevices) {
      const key = device.areaId ?? NO_AREA
      const list = byArea.get(key)
      if (list) list.push(device)
      else byArea.set(key, [device])
    }

    const areaGroups = (areas ?? [])
      .filter((a) => byArea.has(a.id))
      .sort((a, b) => a.name.localeCompare(b.name))
      .map((a) => ({ key: a.id, name: a.name, devices: byArea.get(a.id)! }))

    const noAreaDevices = byArea.get(NO_AREA)
    if (noAreaDevices?.length) {
      areaGroups.push({ key: NO_AREA, name: t('devices.noArea'), devices: noAreaDevices })
    }

    return areaGroups
  }, [filteredDevices, areas, t])

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100">
      <header className="border-b border-slate-800 px-6 py-3 flex items-center justify-between">
        <span className="text-xl font-bold">{t('app.name')}</span>
        <div className="flex items-center gap-4">
          <div className="flex gap-1">
            {(['en', 'az'] as const).map((lng) => (
              <button
                key={lng}
                type="button"
                onClick={() => i18n.changeLanguage(lng)}
                className={`rounded px-2 py-0.5 text-xs uppercase transition-colors ${
                  i18n.resolvedLanguage === lng
                    ? 'bg-slate-100 text-slate-900'
                    : 'bg-slate-800 hover:bg-slate-700'
                }`}
              >
                {lng}
              </button>
            ))}
          </div>
          <Link
            to="/home"
            className="rounded bg-slate-800 hover:bg-slate-700 px-3 py-1 text-sm transition-colors"
          >
            {t('nav.myHome')}
          </Link>
          <Link
            to="/devices"
            className="rounded bg-slate-800 hover:bg-slate-700 px-3 py-1 text-sm transition-colors"
          >
            {t('nav.devices')}
          </Link>
          <Link
            to="/areas"
            className="rounded bg-slate-800 hover:bg-slate-700 px-3 py-1 text-sm transition-colors"
          >
            {t('nav.areas')}
          </Link>
          <Link
            to="/labels"
            className="rounded bg-slate-800 hover:bg-slate-700 px-3 py-1 text-sm transition-colors"
          >
            {t('nav.labels')}
          </Link>
          <Link
            to="/scenes"
            className="rounded bg-slate-800 hover:bg-slate-700 px-3 py-1 text-sm transition-colors"
          >
            {t('nav.scenes')}
          </Link>
          <span className="text-sm text-slate-400">{user?.fullName}</span>
          <button
            type="button"
            onClick={logout}
            className="rounded bg-slate-800 hover:bg-slate-700 px-3 py-1 text-sm transition-colors"
          >
            {t('auth.logout')}
          </button>
        </div>
      </header>

      <main className="p-6 flex flex-col gap-6 max-w-5xl mx-auto">
        {devices && devices.length > 0 && (
          <div className="flex gap-2 overflow-x-auto pb-1">
            {devices.map((device) => (
              <span
                key={device.id}
                className="shrink-0 rounded-full bg-indigo-500/15 text-indigo-400 px-3 py-1 text-xs whitespace-nowrap"
              >
                {badgeIcon(device.type)} {device.name} · {badgeValue(device, t)}
              </span>
            ))}
          </div>
        )}

        {labels && labels.length > 0 && (
          <select
            value={selectedLabelId}
            onChange={(e) => setSelectedLabelId(e.target.value)}
            className="self-start rounded bg-slate-800 border border-slate-700 px-3 py-1 text-sm outline-none focus:border-indigo-500"
          >
            <option value="">{t('dashboard.labelFilter.all')}</option>
            {labels.map((label) => (
              <option key={label.id} value={label.id}>
                {label.name}
              </option>
            ))}
          </select>
        )}

        {scenes && scenes.length > 0 && (
          <div className="flex gap-2 overflow-x-auto pb-1">
            {scenes.map((scene) => (
              <button
                key={scene.id}
                type="button"
                onClick={() => runScene.mutate(scene.id)}
                disabled={runScene.isPending}
                className="shrink-0 rounded-full bg-indigo-500/15 hover:bg-indigo-500/25 text-indigo-400 px-3 py-1 text-xs whitespace-nowrap disabled:opacity-50 transition-colors"
              >
                ▶ {scene.name}
              </button>
            ))}
          </div>
        )}

        {groups.map((group) => (
          <section key={group.key} className="flex flex-col gap-3">
            <h2 className="text-lg font-semibold">{group.name}</h2>
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
              {group.devices.map((device) => (
                <DeviceCard key={device.id} device={device} />
              ))}
            </div>
          </section>
        ))}
      </main>

      <ChatPanel />
    </div>
  )
}
