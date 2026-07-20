import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { isAxiosError } from 'axios'
import { useAreas, useCreateArea, useDeleteArea, useUpdateArea } from '../areas/useAreas'
import type { ApiError } from '../auth/types'

function errorCodeToMessage(t: (key: string) => string, err: unknown): string {
  if (isAxiosError<ApiError>(err) && err.response?.data?.code) {
    return t(`areas.errors.${err.response.data.code}`)
  }
  return t('areas.errors.GENERIC')
}

export default function Areas() {
  const { t } = useTranslation()
  const { data: areas } = useAreas()
  const createArea = useCreateArea()
  const updateArea = useUpdateArea()
  const deleteArea = useDeleteArea()

  const [newName, setNewName] = useState('')
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editingName, setEditingName] = useState('')
  const [error, setError] = useState<string | null>(null)

  const onCreate = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    setError(null)
    try {
      await createArea.mutateAsync(newName)
      setNewName('')
    } catch (err) {
      setError(errorCodeToMessage(t, err))
    }
  }

  const onSaveRename = async (id: string) => {
    setError(null)
    try {
      await updateArea.mutateAsync({ id, name: editingName })
      setEditingId(null)
    } catch (err) {
      setError(errorCodeToMessage(t, err))
    }
  }

  const onDelete = async (id: string) => {
    if (!window.confirm(t('areas.deleteConfirm'))) return
    setError(null)
    try {
      await deleteArea.mutateAsync(id)
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

      <main className="p-6 max-w-lg mx-auto flex flex-col gap-6">
        <h1 className="text-2xl font-semibold">{t('areas.title')}</h1>

        {error && <div className="rounded bg-red-500/15 text-red-400 px-3 py-2 text-sm">{error}</div>}

        <form onSubmit={onCreate} className="flex gap-2">
          <input
            type="text"
            value={newName}
            onChange={(e) => setNewName(e.target.value)}
            placeholder={t('areas.addPlaceholder')}
            required
            className="flex-1 rounded bg-slate-800 border border-slate-700 px-3 py-2 outline-none focus:border-indigo-500"
          />
          <button
            type="submit"
            disabled={createArea.isPending}
            className="rounded bg-indigo-500 hover:bg-indigo-400 disabled:opacity-50 px-4 py-2 text-sm font-medium transition-colors"
          >
            {t('areas.add')}
          </button>
        </form>

        <div className="rounded-xl bg-slate-900 border border-slate-800 p-4">
          <ul className="flex flex-col gap-2">
            {areas?.map((area) => (
              <li key={area.id} className="flex items-center justify-between text-sm gap-2">
                {editingId === area.id ? (
                  <>
                    <input
                      type="text"
                      value={editingName}
                      onChange={(e) => setEditingName(e.target.value)}
                      className="flex-1 rounded bg-slate-800 border border-slate-700 px-2 py-1 outline-none focus:border-indigo-500"
                    />
                    <button
                      type="button"
                      onClick={() => onSaveRename(area.id)}
                      className="rounded bg-indigo-500 hover:bg-indigo-400 px-3 py-1 transition-colors"
                    >
                      {t('areas.rename')}
                    </button>
                  </>
                ) : (
                  <>
                    <span>
                      {area.name}{' '}
                      <span className="text-slate-400">
                        ({area.deviceCount})
                      </span>
                    </span>
                    <div className="flex gap-2">
                      <button
                        type="button"
                        onClick={() => {
                          setEditingId(area.id)
                          setEditingName(area.name)
                        }}
                        className="rounded bg-slate-800 hover:bg-slate-700 px-3 py-1 transition-colors"
                      >
                        {t('areas.rename')}
                      </button>
                      <button
                        type="button"
                        onClick={() => onDelete(area.id)}
                        className="rounded bg-red-500/15 hover:bg-red-500/25 text-red-400 px-3 py-1 transition-colors"
                      >
                        {t('areas.delete')}
                      </button>
                    </div>
                  </>
                )}
              </li>
            ))}
          </ul>
        </div>
      </main>
    </div>
  )
}
