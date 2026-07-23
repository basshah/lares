import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { isAxiosError } from 'axios'
import { useDevices } from '../devices/useDevices'
import { useCreateScene, useDeleteScene, useScenes, useUpdateScene } from '../scenes/useScenes'
import { ACTIONS_BY_TYPE } from '../scenes/deviceActions'
import type { Device } from '../devices/types'
import type { Scene, SceneStep } from '../scenes/types'
import type { SceneStepBody } from '../api/scenes'
import type { ApiError } from '../auth/types'

function errorCodeToMessage(t: (key: string) => string, err: unknown): string {
  if (isAxiosError<ApiError>(err) && err.response?.data?.code) {
    return t(`scenes.errors.${err.response.data.code}`)
  }
  return t('scenes.errors.GENERIC')
}

interface StepDraft {
  deviceId: string
  action: string
  params: Record<string, string>
}

const stepsToDrafts = (steps: SceneStep[]): StepDraft[] =>
  steps.map((s) => ({
    deviceId: s.deviceId,
    action: s.action,
    params: Object.fromEntries(Object.entries(s.params ?? {}).map(([k, v]) => [k, String(v)])),
  }))

const draftsToBody = (drafts: StepDraft[], devices: Device[]): SceneStepBody[] =>
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

function StepEditor({
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

interface EditState {
  id: string
  name: string
  steps: StepDraft[]
}

export default function Scenes() {
  const { t } = useTranslation()
  const { data: scenes } = useScenes()
  const { data: devices } = useDevices()
  const actionableDevices = (devices ?? []).filter((d) => ACTIONS_BY_TYPE[d.type].length > 0)
  const createScene = useCreateScene()
  const updateScene = useUpdateScene()
  const deleteScene = useDeleteScene()

  const [newName, setNewName] = useState('')
  const [newSteps, setNewSteps] = useState<StepDraft[]>([])
  const [editing, setEditing] = useState<EditState | null>(null)
  const [error, setError] = useState<string | null>(null)

  const onCreate = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    setError(null)
    try {
      await createScene.mutateAsync({ name: newName, steps: draftsToBody(newSteps, actionableDevices) })
      setNewName('')
      setNewSteps([])
    } catch (err) {
      setError(errorCodeToMessage(t, err))
    }
  }

  const startEdit = (scene: Scene) => {
    setEditing({ id: scene.id, name: scene.name, steps: stepsToDrafts(scene.steps) })
  }

  const onSaveEdit = async () => {
    if (!editing) return
    setError(null)
    try {
      await updateScene.mutateAsync({
        id: editing.id,
        body: { name: editing.name, steps: draftsToBody(editing.steps, actionableDevices) },
      })
      setEditing(null)
    } catch (err) {
      setError(errorCodeToMessage(t, err))
    }
  }

  const onDelete = async (id: string) => {
    if (!window.confirm(t('scenes.deleteConfirm'))) return
    setError(null)
    try {
      await deleteScene.mutateAsync(id)
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
        <h1 className="text-2xl font-semibold">{t('scenes.title')}</h1>

        {error && <div className="rounded bg-red-500/15 text-red-400 px-3 py-2 text-sm">{error}</div>}

        <form onSubmit={onCreate} className="rounded-xl bg-slate-900 border border-slate-800 p-4 flex flex-col gap-3">
          <label className="flex flex-col gap-1 text-sm">
            <span className="text-slate-300">{t('scenes.nameLabel')}</span>
            <input
              type="text"
              value={newName}
              onChange={(e) => setNewName(e.target.value)}
              placeholder={t('scenes.addPlaceholder')}
              required
              className="rounded bg-slate-800 border border-slate-700 px-3 py-2 outline-none focus:border-indigo-500"
            />
          </label>
          <StepEditor devices={actionableDevices} steps={newSteps} onChange={setNewSteps} />
          <button
            type="submit"
            disabled={createScene.isPending || newSteps.length === 0}
            className="self-start rounded bg-indigo-500 hover:bg-indigo-400 disabled:opacity-50 px-4 py-2 text-sm font-medium transition-colors"
          >
            {t('scenes.add')}
          </button>
        </form>

        <div className="rounded-xl bg-slate-900 border border-slate-800 p-4">
          <ul className="flex flex-col gap-3">
            {scenes?.map((scene) => (
              <li key={scene.id} className="rounded bg-slate-800 p-3">
                {editing?.id === scene.id ? (
                  <div className="flex flex-col gap-2">
                    <input
                      type="text"
                      value={editing.name}
                      onChange={(e) => setEditing({ ...editing, name: e.target.value })}
                      className="rounded bg-slate-900 border border-slate-700 px-2 py-1 outline-none focus:border-indigo-500"
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
                        {t('scenes.save')}
                      </button>
                      <button
                        type="button"
                        onClick={() => setEditing(null)}
                        className="rounded bg-slate-700 hover:bg-slate-600 px-3 py-1 text-sm transition-colors"
                      >
                        {t('scenes.cancel')}
                      </button>
                    </div>
                  </div>
                ) : (
                  <div className="flex items-center justify-between gap-2">
                    <div className="flex flex-col gap-1">
                      <span className="font-medium">{scene.name}</span>
                      <div className="text-sm text-slate-400">
                        {scene.steps.map((s) => `${s.deviceName} · ${t(`scenes.action.${s.action}`)}`).join(', ')}
                      </div>
                    </div>
                    <div className="flex gap-2">
                      <button
                        type="button"
                        onClick={() => startEdit(scene)}
                        className="rounded bg-slate-700 hover:bg-slate-600 px-3 py-1 text-sm transition-colors"
                      >
                        {t('scenes.edit')}
                      </button>
                      <button
                        type="button"
                        onClick={() => onDelete(scene.id)}
                        className="rounded bg-red-500/15 hover:bg-red-500/25 text-red-400 px-3 py-1 text-sm transition-colors"
                      >
                        {t('scenes.delete')}
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
