import { useTranslation } from 'react-i18next'
import { ACTIONS_BY_TYPE } from '../scenes/deviceActions'
import type { Device } from '../devices/types'

export interface StepDraft {
  deviceId: string
  action: string
  params: Record<string, string>
}

// Structurally matches both SceneStep and AutomationStep response shapes.
export interface StepLike {
  deviceId: string
  action: string
  params: Record<string, unknown> | null
}

// Structurally matches both SceneStepBody and AutomationStepBody request shapes.
export interface StepBody {
  deviceId: string
  action: string
  params?: Record<string, unknown> | null
}

export const stepsToDrafts = (steps: StepLike[]): StepDraft[] =>
  steps.map((s) => ({
    deviceId: s.deviceId,
    action: s.action,
    params: Object.fromEntries(Object.entries(s.params ?? {}).map(([k, v]) => [k, String(v)])),
  }))

export const draftsToBody = (drafts: StepDraft[], devices: Device[]): StepBody[] =>
  drafts.map((draft) => {
    const device = devices.find((d) => d.id === draft.deviceId)
    const spec = device ? ACTIONS_BY_TYPE[device.type].find((a) => a.action === draft.action) : undefined
    if (!spec || spec.params.length === 0) {
      return { deviceId: draft.deviceId, action: draft.action }
    }
    const params: Record<string, unknown> = {}
    for (const p of spec.params) {
      params[p.key] = p.kind === 'enum' ? draft.params[p.key] : Number(draft.params[p.key])
    }
    return { deviceId: draft.deviceId, action: draft.action, params }
  })

export function StepEditor({
  devices,
  steps,
  onChange,
}: {
  devices: Device[]
  steps: StepDraft[]
  onChange: (steps: StepDraft[]) => void
}) {
  const { t } = useTranslation()

  const addStep = () => {
    const firstDevice = devices[0]
    if (!firstDevice) return
    const firstAction = ACTIONS_BY_TYPE[firstDevice.type][0]
    onChange([...steps, { deviceId: firstDevice.id, action: firstAction?.action ?? '', params: {} }])
  }

  const updateStep = (index: number, patch: Partial<StepDraft>) => {
    onChange(steps.map((s, i) => (i === index ? { ...s, ...patch } : s)))
  }

  const removeStep = (index: number) => onChange(steps.filter((_, i) => i !== index))

  return (
    <div className="flex flex-col gap-2">
      {steps.map((step, i) => {
        const device = devices.find((d) => d.id === step.deviceId)
        const actions = device ? ACTIONS_BY_TYPE[device.type] : []
        const actionSpec = actions.find((a) => a.action === step.action)
        return (
          <div key={i} className="flex flex-wrap items-center gap-2 rounded bg-slate-800 p-2">
            <select
              value={step.deviceId}
              onChange={(e) => {
                const newDevice = devices.find((d) => d.id === e.target.value)
                const newAction = newDevice ? (ACTIONS_BY_TYPE[newDevice.type][0]?.action ?? '') : ''
                updateStep(i, { deviceId: e.target.value, action: newAction, params: {} })
              }}
              className="rounded bg-slate-900 border border-slate-700 px-2 py-1 text-sm outline-none focus:border-indigo-500"
            >
              {devices.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name}
                </option>
              ))}
            </select>
            <select
              value={step.action}
              onChange={(e) => updateStep(i, { action: e.target.value, params: {} })}
              className="rounded bg-slate-900 border border-slate-700 px-2 py-1 text-sm outline-none focus:border-indigo-500"
            >
              {actions.map((a) => (
                <option key={a.action} value={a.action}>
                  {t(`scenes.action.${a.action}`)}
                </option>
              ))}
            </select>
            {actionSpec?.params.map((p) =>
              p.kind === 'enum' ? (
                <select
                  key={p.key}
                  value={step.params[p.key] ?? p.enumOptions?.[0] ?? ''}
                  onChange={(e) => updateStep(i, { params: { ...step.params, [p.key]: e.target.value } })}
                  className="rounded bg-slate-900 border border-slate-700 px-2 py-1 text-sm outline-none focus:border-indigo-500"
                >
                  {p.enumOptions!.map((opt) => (
                    <option key={opt} value={opt}>
                      {t(`devices.mode.${opt}`)}
                    </option>
                  ))}
                </select>
              ) : (
                <input
                  key={p.key}
                  type="number"
                  value={step.params[p.key] ?? ''}
                  onChange={(e) => updateStep(i, { params: { ...step.params, [p.key]: e.target.value } })}
                  className="w-20 rounded bg-slate-900 border border-slate-700 px-2 py-1 text-sm outline-none focus:border-indigo-500"
                />
              ),
            )}
            <button
              type="button"
              onClick={() => removeStep(i)}
              className="rounded bg-red-500/15 hover:bg-red-500/25 text-red-400 px-2 py-1 text-xs transition-colors"
            >
              {t('scenes.removeStep')}
            </button>
          </div>
        )
      })}
      <button
        type="button"
        onClick={addStep}
        disabled={devices.length === 0}
        className="self-start rounded bg-slate-800 hover:bg-slate-700 disabled:opacity-50 px-3 py-1 text-sm transition-colors"
      >
        {t('scenes.addStep')}
      </button>
    </div>
  )
}
