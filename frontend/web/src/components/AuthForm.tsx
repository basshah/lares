import type { FormEvent, ReactNode } from 'react'
import { useTranslation } from 'react-i18next'

interface AuthFormProps {
  title: string
  error: string | null
  isSubmitting: boolean
  submitLabel: string
  onSubmit: (e: FormEvent<HTMLFormElement>) => void
  footer: ReactNode
  children: ReactNode
}

export function AuthForm({
  title,
  error,
  isSubmitting,
  submitLabel,
  onSubmit,
  footer,
  children,
}: AuthFormProps) {
  const { t } = useTranslation()

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100 flex items-center justify-center p-4">
      <div className="w-full max-w-sm">
        <h1 className="text-3xl font-bold text-center">{t('app.name')}</h1>
        <p className="mt-1 mb-8 text-center text-sm text-slate-400">{t('app.tagline')}</p>

        <form
          onSubmit={onSubmit}
          className="rounded-xl bg-slate-900 border border-slate-800 p-6 flex flex-col gap-4"
        >
          <h2 className="text-lg font-semibold">{title}</h2>

          {error && (
            <div className="rounded bg-red-500/15 text-red-400 px-3 py-2 text-sm">{error}</div>
          )}

          {children}

          <button
            type="submit"
            disabled={isSubmitting}
            className="mt-2 rounded bg-indigo-500 hover:bg-indigo-400 disabled:opacity-50 py-2 font-medium transition-colors"
          >
            {submitLabel}
          </button>

          <div className="text-sm text-slate-400 text-center">{footer}</div>
        </form>
      </div>
    </div>
  )
}

export function AuthInput(props: {
  label: string
  type: string
  value: string
  onChange: (value: string) => void
  autoComplete?: string
}) {
  return (
    <label className="flex flex-col gap-1 text-sm">
      <span className="text-slate-300">{props.label}</span>
      <input
        type={props.type}
        value={props.value}
        onChange={(e) => props.onChange(e.target.value)}
        autoComplete={props.autoComplete}
        required
        className="rounded bg-slate-800 border border-slate-700 px-3 py-2 outline-none focus:border-indigo-500"
      />
    </label>
  )
}
