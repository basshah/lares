import { useEffect, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { isAxiosError } from 'axios'
import { AuthForm, AuthInput } from '../components/AuthForm'
import { useCreateHome, useJoinHome, useMyHome } from '../home/useHome'
import type { ApiError } from '../auth/types'

function errorCodeToMessage(t: (key: string) => string, err: unknown): string {
  if (isAxiosError<ApiError>(err) && err.response?.data?.code) {
    return t(`myHome.errors.${err.response.data.code}`)
  }
  return t('myHome.errors.GENERIC')
}

export default function HomeSetup() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { data: home, isLoading } = useMyHome()
  const createHome = useCreateHome()
  const joinHome = useJoinHome()

  const [mode, setMode] = useState<'create' | 'join'>('create')
  const [name, setName] = useState('')
  const [inviteCode, setInviteCode] = useState('')
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!isLoading && home) navigate('/', { replace: true })
  }, [isLoading, home, navigate])

  const isSubmitting = createHome.isPending || joinHome.isPending

  const onSubmit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    setError(null)
    try {
      if (mode === 'create') {
        await createHome.mutateAsync(name)
      } else {
        await joinHome.mutateAsync(inviteCode)
      }
      navigate('/', { replace: true })
    } catch (err) {
      setError(errorCodeToMessage(t, err))
    }
  }

  return (
    <AuthForm
      title={t('myHome.setup.title')}
      error={error}
      isSubmitting={isSubmitting}
      submitLabel={mode === 'create' ? t('myHome.setup.createButton') : t('myHome.setup.joinButton')}
      onSubmit={onSubmit}
      footer={
        <button
          type="button"
          onClick={() => {
            setError(null)
            setMode(mode === 'create' ? 'join' : 'create')
          }}
          className="text-indigo-400 hover:underline"
        >
          {mode === 'create' ? t('myHome.setup.joinTab') : t('myHome.setup.createTab')}
        </button>
      }
    >
      {mode === 'create' ? (
        <AuthInput label={t('myHome.setup.nameLabel')} type="text" value={name} onChange={setName} />
      ) : (
        <AuthInput
          label={t('myHome.setup.inviteCodeLabel')}
          type="text"
          value={inviteCode}
          onChange={(value) => setInviteCode(value.toUpperCase())}
        />
      )}
    </AuthForm>
  )
}
