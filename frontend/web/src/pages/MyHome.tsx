import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useMyHome, useRegenerateInvite, useLeaveHome } from '../home/useHome'

export default function MyHome() {
  const { t } = useTranslation()
  const { data: home } = useMyHome()
  const regenerateInvite = useRegenerateInvite()
  const leaveHome = useLeaveHome()

  if (!home) return null

  const onRegenerate = () => {
    if (window.confirm(t('myHome.regenerateConfirm'))) regenerateInvite.mutate()
  }

  const onLeave = () => {
    if (window.confirm(t('myHome.leaveConfirm'))) leaveHome.mutate()
  }

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100">
      <header className="border-b border-slate-800 px-6 py-3 flex items-center justify-between">
        <Link to="/" className="text-xl font-bold">
          {t('app.name')}
        </Link>
      </header>

      <main className="p-6 max-w-lg mx-auto flex flex-col gap-6">
        <h1 className="text-2xl font-semibold">{home.name}</h1>

        {home.inviteCode && (
          <div className="rounded-xl bg-slate-900 border border-slate-800 p-4 flex flex-col gap-2">
            <span className="text-sm text-slate-400">{t('myHome.inviteCode')}</span>
            <div className="flex items-center gap-2">
              <span className="font-mono text-lg rounded bg-slate-800 px-3 py-1">{home.inviteCode}</span>
              <button
                type="button"
                onClick={() => navigator.clipboard.writeText(home.inviteCode!)}
                className="rounded bg-slate-800 hover:bg-slate-700 px-3 py-1 text-sm transition-colors"
              >
                {t('myHome.copy')}
              </button>
              <button
                type="button"
                onClick={onRegenerate}
                disabled={regenerateInvite.isPending}
                className="rounded bg-slate-800 hover:bg-slate-700 disabled:opacity-50 px-3 py-1 text-sm transition-colors"
              >
                {t('myHome.regenerate')}
              </button>
            </div>
          </div>
        )}

        <div className="rounded-xl bg-slate-900 border border-slate-800 p-4 flex flex-col gap-3">
          <span className="text-sm text-slate-400">{t('myHome.members')}</span>
          <ul className="flex flex-col gap-2">
            {home.members.map((member) => (
              <li key={member.userId} className="flex items-center justify-between text-sm">
                <div>
                  <div>{member.fullName}</div>
                  <div className="text-slate-400">{member.email}</div>
                </div>
                <span
                  className={`rounded-full px-3 py-0.5 text-xs ${
                    member.role === 'Owner'
                      ? 'bg-indigo-500/15 text-indigo-400'
                      : 'bg-slate-500/15 text-slate-400'
                  }`}
                >
                  {t(`myHome.role.${member.role}`)}
                </span>
              </li>
            ))}
          </ul>
        </div>

        {home.role === 'Member' && (
          <button
            type="button"
            onClick={onLeave}
            disabled={leaveHome.isPending}
            className="self-start rounded bg-red-500/15 hover:bg-red-500/25 text-red-400 disabled:opacity-50 px-4 py-2 text-sm transition-colors"
          >
            {t('myHome.leave')}
          </button>
        )}
      </main>
    </div>
  )
}
