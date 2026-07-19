import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useAuth } from '../auth/AuthContext'
import { AuthForm, AuthInput } from '../components/AuthForm'
import { errorCodeToMessage } from './Login'

export default function Register() {
  const { t } = useTranslation()
  const { register } = useAuth()
  const navigate = useNavigate()

  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const onSubmit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault()
    setError(null)
    setIsSubmitting(true)
    try {
      await register(email, password, fullName)
      navigate('/')
    } catch (err) {
      setError(errorCodeToMessage(t, err))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <AuthForm
      title={t('auth.registerTitle')}
      error={error}
      isSubmitting={isSubmitting}
      submitLabel={t('auth.registerButton')}
      onSubmit={onSubmit}
      footer={
        <>
          {t('auth.haveAccount')}{' '}
          <Link to="/login" className="text-indigo-400 hover:underline">
            {t('auth.loginLink')}
          </Link>
        </>
      }
    >
      <AuthInput label={t('auth.fullName')} type="text" value={fullName} onChange={setFullName} />
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
        autoComplete="new-password"
      />
    </AuthForm>
  )
}
