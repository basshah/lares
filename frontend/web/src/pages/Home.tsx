import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { api } from '../api/client'
import { useAuth } from '../auth/AuthContext'

export default function Home() {
  const { t, i18n } = useTranslation()
  const { user, logout } = useAuth()

  const health = useQuery({
    queryKey: ['health'],
    queryFn: async () => (await api.get('/api/health')).data,
    retry: false,
  })

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

      <main className="p-6 flex flex-col items-center gap-4 text-center">
        <h1 className="mt-16 text-3xl font-semibold">
          {t('home.welcome', { name: user?.fullName })}
        </h1>
        <p className="text-slate-400">{t('home.placeholder')}</p>
        <div
          className={`rounded-full px-4 py-1 text-sm ${
            health.isSuccess
              ? 'bg-emerald-500/15 text-emerald-400'
              : health.isPending
                ? 'bg-slate-500/15 text-slate-400'
                : 'bg-red-500/15 text-red-400'
          }`}
        >
          {health.isPending
            ? t('health.checking')
            : health.isSuccess
              ? t('health.ok')
              : t('health.fail')}
        </div>
      </main>
    </div>
  )
}
