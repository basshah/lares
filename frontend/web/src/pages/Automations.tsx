import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import type { TFunction } from 'i18next'
import { isAxiosError } from 'axios'
import { useDevices } from '../devices/useDevices'
import {
  useCreateAutomation,
  useDeleteAutomation,
  useAutomations,
  useRunAutomation,
  useSetAutomationEnabled,
  useUpdateAutomation,
} from '../automations/useAutomations'
import { ACTIONS_BY_TYPE } from '../scenes/deviceActions'
import { STATES_BY_TYPE } from '../automations/deviceStates'
import { StepEditor, stepsToDrafts, draftsToBody, type StepDraft } from '../steps/StepEditor'
import type { Device } from '../devices/types'
import type { Automation, AutomationDayOfWeek, AutomationTriggerType } from '../automations/types'
import type { AutomationBody } from '../api/automations'
import type { ApiError } from '../auth/types'

const ALL_DAYS: AutomationDayOfWeek[] = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday']

function errorCodeToMessage(t: (key: string) => string, err: unknown): string {
  if (isAxiosError<ApiError>(err) && err.response?.data?.code) {
    return t(`automations.errors.${err.response.data.code}`)
  }
  return t('automations.errors.GENERIC')
}

function deviceStateLabel(t: TFunction, state: string): string {
  if (state === 'on' || state === 'off' || state === 'online' || state === 'offline') {
    return t(`dashboard.state.${state}`)
  }
  return t(`automations.deviceState.${state}`)
}

interface AutomationDraft {
  name: string
  isEnabled: boolean
  triggerType: AutomationTriggerType
  time: string // "HH:mm"
  days: AutomationDayOfWeek[]
  triggerDeviceId: string
  triggerState: string
  steps: StepDraft[]
}

const emptyDraft = (): AutomationDraft => ({
  name: '',
  isEnabled: true,
  triggerType: 'Time',
  time: '',
  days: [],
  triggerDeviceId: '',
  triggerState: '',
  steps: [],
})

const automationToDraft = (a: Automation): AutomationDraft => ({
  name: a.name,
  isEnabled: a.isEnabled,
  triggerType: a.triggerType,
  time: a.triggerTimeOfDay ? a.triggerTimeOfDay.slice(0, 5) : '',
  days: a.triggerDaysOfWeek ?? [],
  triggerDeviceId: a.triggerDeviceId ?? '',
  triggerState: a.triggerState ?? '',
  steps: stepsToDrafts(a.steps),
})

const draftToBody = (draft: AutomationDraft, actionableDevices: Device[]): AutomationBody => ({
  name: draft.name,
  isEnabled: draft.isEnabled,
  triggerType: draft.triggerType,
  triggerTimeOfDay: draft.triggerType === 'Time' && draft.time ? `${draft.time}:00` : null,
  triggerDaysOfWeek: draft.triggerType === 'Time' && draft.days.length > 0 ? draft.days : null,
  triggerDeviceId: draft.triggerType === 'DeviceState' ? draft.triggerDeviceId || null : null,
  triggerState: draft.triggerType === 'DeviceState' ? draft.triggerState || null : null,
  steps: draftsToBody(draft.steps, actionableDevices),
})

function triggerSummary(t: TFunction, a: Automation): string {
  if (a.triggerType === 'Time') {
    const time = a.triggerTimeOfDay?.slice(0, 5) ?? '?'
    const days = a.triggerDaysOfWeek?.length
      ? a.triggerDaysOfWeek.map((d) => t(`automations.day.${d}`)).join(', ')
      : null
    return days ? `${days} ${time}` : `${t('automations.trigger.typeTime')} ${time}`
  }
  return `${a.triggerDeviceName ?? '?'} → ${deviceStateLabel(t, a.triggerState ?? '')}`
}

function TriggerFields({
  draft,
  onChange,
  triggerableDevices,
}: {
  draft: AutomationDraft
  onChange: (patch: Partial<AutomationDraft>) => void
  triggerableDevices: Device[]
}) {
  const { t } = useTranslation()
  const selectedTriggerDevice = triggerableDevices.find((d) => d.id === draft.triggerDeviceId)
  const stateOptions = selectedTriggerDevice ? STATES_BY_TYPE[selectedTriggerDevice.type] : []

  return (
    <div className="flex flex-col gap-2">
      <label className="flex flex-col gap-1 text-sm">
        <span className="text-slate-300">{t('automations.trigger.typeLabel')}</span>
        <select
          value={draft.triggerType}
          onChange={(e) => onChange({ triggerType: e.target.value as AutomationTriggerType })}
          className="rounded bg-slate-900 border border-slate-700 px-2 py-1 outline-none focus:border-indigo-500"
        >
          <option value="Time">{t('automations.trigger.typeTime')}</option>
          <option value="DeviceState">{t('automations.trigger.typeDeviceState')}</option>
        </select>
      </label>

      {draft.triggerType === 'Time' ? (
        <>
          <label className="flex flex-col gap-1 text-sm">
            <span className="text-slate-300">{t('automations.trigger.timeLabel')}</span>
            <input
              type="time"
              value={draft.time}
              onChange={(e) => onChange({ time: e.target.value })}
              required
              className="rounded bg-slate-900 border border-slate-700 px-2 py-1 outline-none focus:border-indigo-500"
            />
          </label>
          <div className="flex flex-col gap-1 text-sm">
            <span className="text-slate-300">{t('automations.trigger.daysLabel')}</span>
            <div className="flex flex-wrap gap-2">
              {ALL_DAYS.map((day) => (
                <label key={day} className="flex items-center gap-1">
                  <input
                    type="checkbox"
                    checked={draft.days.includes(day)}
                    onChange={() =>
                      onChange({
                        days: draft.days.includes(day) ? draft.days.filter((d) => d !== day) : [...draft.days, day],
                      })
                    }
                  />
                  {t(`automations.day.${day}`)}
                </label>
              ))}
            </div>
          </div>
        </>
      ) : (
        <>
          <label className="flex flex-col gap-1 text-sm">
            <span className="text-slate-300">{t('automations.trigger.deviceLabel')}</span>
            <select
              value={draft.triggerDeviceId}
              onChange={(e) => {
                const device = triggerableDevices.find((d) => d.id === e.target.value)
                const firstState = device ? (STATES_BY_TYPE[device.type][0] ?? '') : ''
                onChange({ triggerDeviceId: e.target.value, triggerState: firstState })
              }}
              className="rounded bg-slate-900 border border-slate-700 px-2 py-1 outline-none focus:border-indigo-500"
            >
              <option value="">—</option>
              {triggerableDevices.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name}
                </option>
              ))}
            </select>
          </label>
          <label className="flex flex-col gap-1 text-sm">
            <span className="text-slate-300">{t('automations.trigger.stateLabel')}</span>
            <select
              value={draft.triggerState}
              onChange={(e) => onChange({ triggerState: e.target.value })}
              className="rounded bg-slate-900 border border-slate-700 px-2 py-1 outline-none focus:border-indigo-500"
            >
              {stateOptions.map((s) => (
                <option key={s} value={s}>
                  {deviceStateLabel(t, s)}
                </option>
              ))}
            </select>
          </label>
        </>
      )}
    </div>
  )
}

interface EditState extends AutomationDraft {
  id: string
}

export default function Automations() {
  const { t } = useTranslation()
  const { data: automations } = useAutomations()
  const { data: devices } = useDevices()
  const actionableDevices = (devices ?? []).filter((d) => ACTIONS_BY_TYPE[d.type].length > 0)
  const triggerableDevices = (devices ?? []).filter((d) => STATES_BY_TYPE[d.type].length > 0)

  const createAutomation = useCreateAutomation()
  const updateAutomation = useUpdateAutomation()
  const deleteAutomation = useDeleteAutomation()
  const setEnabled = useSetAutomationEnabled()
  const runAutomation = useRunAutomation()

  const [draft, setDraft] = useState<AutomationDraft>(emptyDraft())
  const [editing, setEditing] = useState<EditState | null>(null)
  const [error, setError] = useState<string | null>(null)

  const onCreate = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    setError(null)
    try {
      await createAutomation.mutateAsync(draftToBody(draft, actionableDevices))
      setDraft(emptyDraft())
    } catch (err) {
      setError(errorCodeToMessage(t, err))
    }
  }

  const startEdit = (automation: Automation) => {
    setEditing({ id: automation.id, ...automationToDraft(automation) })
  }

  const onSaveEdit = async () => {
    if (!editing) return
    setError(null)
    try {
      await updateAutomation.mutateAsync({ id: editing.id, body: draftToBody(editing, actionableDevices) })
      setEditing(null)
    } catch (err) {
      setError(errorCodeToMessage(t, err))
    }
  }

  const onDelete = async (id: string) => {
    if (!window.confirm(t('automations.deleteConfirm'))) return
    setError(null)
    try {
      await deleteAutomation.mutateAsync(id)
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
        <h1 className="text-2xl font-semibold">{t('automations.title')}</h1>

        {error && <div className="rounded bg-red-500/15 text-red-400 px-3 py-2 text-sm">{error}</div>}

        <form onSubmit={onCreate} className="rounded-xl bg-slate-900 border border-slate-800 p-4 flex flex-col gap-3">
          <label className="flex flex-col gap-1 text-sm">
            <span className="text-slate-300">{t('automations.nameLabel')}</span>
            <input
              type="text"
              value={draft.name}
              onChange={(e) => setDraft({ ...draft, name: e.target.value })}
              placeholder={t('automations.addPlaceholder')}
              required
              className="rounded bg-slate-800 border border-slate-700 px-3 py-2 outline-none focus:border-indigo-500"
            />
          </label>
          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={draft.isEnabled}
              onChange={(e) => setDraft({ ...draft, isEnabled: e.target.checked })}
            />
            {t('automations.enabled')}
          </label>
          <TriggerFields draft={draft} onChange={(patch) => setDraft({ ...draft, ...patch })} triggerableDevices={triggerableDevices} />
          <StepEditor devices={actionableDevices} steps={draft.steps} onChange={(steps) => setDraft({ ...draft, steps })} />
          <button
            type="submit"
            disabled={createAutomation.isPending || draft.steps.length === 0}
            className="self-start rounded bg-indigo-500 hover:bg-indigo-400 disabled:opacity-50 px-4 py-2 text-sm font-medium transition-colors"
          >
            {t('automations.add')}
          </button>
        </form>

        <div className="rounded-xl bg-slate-900 border border-slate-800 p-4">
          <ul className="flex flex-col gap-3">
            {automations?.map((automation) => (
              <li key={automation.id} className="rounded bg-slate-800 p-3">
                {editing?.id === automation.id ? (
                  <div className="flex flex-col gap-2">
                    <input
                      type="text"
                      value={editing.name}
                      onChange={(e) => setEditing({ ...editing, name: e.target.value })}
                      className="rounded bg-slate-900 border border-slate-700 px-2 py-1 outline-none focus:border-indigo-500"
                    />
                    <label className="flex items-center gap-2 text-sm">
                      <input
                        type="checkbox"
                        checked={editing.isEnabled}
                        onChange={(e) => setEditing({ ...editing, isEnabled: e.target.checked })}
                      />
                      {t('automations.enabled')}
                    </label>
                    <TriggerFields
                      draft={editing}
                      onChange={(patch) => setEditing({ ...editing, ...patch })}
                      triggerableDevices={triggerableDevices}
                    />
                    <StepEditor
                      devices={actionableDevices}
                      steps={editing.steps}
                      onChange={(steps) => setEditing({ ...editing, steps })}
                    />
                    <div className="flex gap-2">
                      <button
                        type="button"
                        onClick={onSaveEdit}
                        className="rounded bg-indigo-500 hover:bg-indigo-400 px-3 py-1 text-sm transition-colors"
                      >
                        {t('automations.save')}
                      </button>
                      <button
                        type="button"
                        onClick={() => setEditing(null)}
                        className="rounded bg-slate-700 hover:bg-slate-600 px-3 py-1 text-sm transition-colors"
                      >
                        {t('automations.cancel')}
                      </button>
                    </div>
                  </div>
                ) : (
                  <div className="flex items-center justify-between gap-2">
                    <div className="flex flex-col gap-1">
                      <div className="flex items-center gap-2">
                        <span className="font-medium">{automation.name}</span>
                        {!automation.isEnabled && (
                          <span className="rounded-full bg-slate-700 text-slate-400 px-2 py-0.5 text-xs">
                            {t('automations.disabled')}
                          </span>
                        )}
                      </div>
                      <div className="text-sm text-slate-400">{triggerSummary(t, automation)}</div>
                      <div className="text-sm text-slate-400">
                        {automation.steps.map((s) => `${s.deviceName} · ${t(`scenes.action.${s.action}`)}`).join(', ')}
                      </div>
                    </div>
                    <div className="flex items-center gap-2">
                      <label className="flex items-center gap-1 text-xs text-slate-400">
                        <input
                          type="checkbox"
                          checked={automation.isEnabled}
                          onChange={(e) => setEnabled.mutate({ id: automation.id, isEnabled: e.target.checked })}
                        />
                        {t('automations.enabled')}
                      </label>
                      <button
                        type="button"
                        onClick={() => runAutomation.mutate(automation.id)}
                        disabled={runAutomation.isPending}
                        className="rounded bg-indigo-500/15 hover:bg-indigo-500/25 text-indigo-400 px-3 py-1 text-sm transition-colors"
                      >
                        {t('automations.runNow')}
                      </button>
                      <button
                        type="button"
                        onClick={() => startEdit(automation)}
                        className="rounded bg-slate-700 hover:bg-slate-600 px-3 py-1 text-sm transition-colors"
                      >
                        {t('automations.edit')}
                      </button>
                      <button
                        type="button"
                        onClick={() => onDelete(automation.id)}
                        className="rounded bg-red-500/15 hover:bg-red-500/25 text-red-400 px-3 py-1 text-sm transition-colors"
                      >
                        {t('automations.delete')}
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
