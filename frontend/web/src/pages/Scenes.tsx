import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { isAxiosError } from 'axios'
import { useDevices } from '../devices/useDevices'
import { useCreateScene, useDeleteScene, useScenes, useUpdateScene } from '../scenes/useScenes'
import { ACTIONS_BY_TYPE } from '../scenes/deviceActions'
import { StepEditor, stepsToDrafts, draftsToBody, type StepDraft } from '../steps/StepEditor'
import type { Scene } from '../scenes/types'
import type { ApiError } from '../auth/types'

function errorCodeToMessage(t: (key: string) => string, err: unknown): string {
  if (isAxiosError<ApiError>(err) && err.response?.data?.code) {
    return t(`scenes.errors.${err.response.data.code}`)
  }
  return t('scenes.errors.GENERIC')
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
