import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api } from './api/client'

function App() {
  const { t, i18n } = useTranslation()

  const health = useQuery({
    queryKey: ['health'],
    queryFn: async () => (await api.get('/api/health')).data,
    retry: false,
  })

  const healthLabel = health.isPending
    ? t('health.checking')
    : health.isSuccess
      ? t('health.ok')
      : t('health.fail')

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100 flex flex-col items-center justify-center gap-6">
      <header className="text-center">
        <h1 className="text-5xl font-bold tracking-tight">{t('app.name')}</h1>
        <p className="mt-2 text-slate-400">{t('app.tagline')}</p>
      </header>

      <div
        className={`rounded-full px-4 py-1 text-sm ${
          health.isSuccess
            ? 'bg-emerald-500/15 text-emerald-400'
            : health.isPending
              ? 'bg-slate-500/15 text-slate-400'
              : 'bg-red-500/15 text-red-400'
        }`}
      >
        {healthLabel}
      </div>

      <div className="flex gap-2">
        {(['en', 'az'] as const).map((lng) => (
          <button
            key={lng}
            type="button"
            onClick={() => i18n.changeLanguage(lng)}
            className={`rounded px-3 py-1 text-sm uppercase transition-colors ${
              i18n.resolvedLanguage === lng
                ? 'bg-slate-100 text-slate-900'
                : 'bg-slate-800 hover:bg-slate-700'
            }`}
          >
            {lng}
          </button>
        ))}
      </div>
    </div>
  )
}

export default App
