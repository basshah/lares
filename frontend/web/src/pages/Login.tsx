import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { isAxiosError } from 'axios'
import { useAuth } from '../auth/AuthContext'
import { AuthForm, AuthInput } from '../components/AuthForm'
import type { ApiError } from '../auth/types'

export function errorCodeToMessage(t: (key: string) => string, err: unknown): string {
  if (isAxiosError<ApiError>(err) && err.response?.data?.code) {
    return t(`auth.errors.${err.response.data.code}`)
  }
  return t('auth.errors.GENERIC')
}

export default function Login() {
  const { t } = useTranslation()
  const { login } = useAuth()
  const navigate = useNavigate()

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const onSubmit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    setError(null)
    setIsSubmitting(true)
    try {
      await login(email, password)
      navigate('/')
    } catch (err) {
      setError(errorCodeToMessage(t, err))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <AuthForm
      title={t('auth.loginTitle')}
      error={error}
      isSubmitting={isSubmitting}
      submitLabel={t('auth.loginButton')}
      onSubmit={onSubmit}
      footer={
        <>
          {t('auth.noAccount')}{' '}
          <Link to="/register" className="text-indigo-400 hover:underline">
            {t('auth.registerLink')}
          </Link>
        </>
      }
    >
      <AuthInput
        label={t('auth.email')}
        type="email"
        value={email}
        onChange={setEmail}
        autoComplete="email"
      />
      <AuthInput
        label={t('auth.password')}
        type="password"
        value={password}
        onChange={setPassword}
        autoComplete="current-password"
      />
    </AuthForm>
  )
}
