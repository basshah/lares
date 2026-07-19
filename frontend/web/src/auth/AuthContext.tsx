/* eslint-disable react-refresh/only-export-components */
import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { api } from '../api/client'
import { tokenStorage } from './storage'
import type { AuthResponse, User } from './types'

interface AuthContextValue {
  user: User | null
  isInitializing: boolean
  login: (email: string, password: string) => Promise<void>
  register: (email: string, password: string, fullName: string) => Promise<void>
  logout: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(() => tokenStorage.getUser())
  const [isInitializing, setIsInitializing] = useState(!!tokenStorage.getAccessToken())

  useEffect(() => {
    if (!tokenStorage.getAccessToken()) return
    // Validate the stored session; the interceptor refreshes an expired access token.
    api
      .get<User>('/api/auth/me')
      .then(({ data }) => setUser(data))
      .catch(() => {
        tokenStorage.clear()
        setUser(null)
      })
      .finally(() => setIsInitializing(false))
  }, [])

  const applyAuth = (data: AuthResponse) => {
    tokenStorage.save(data.accessToken, data.refreshToken, data.user)
    setUser(data.user)
  }

  const login = async (email: string, password: string) => {
    const { data } = await api.post<AuthResponse>('/api/auth/login', { email, password })
    applyAuth(data)
  }

  const register = async (email: string, password: string, fullName: string) => {
    const { data } = await api.post<AuthResponse>('/api/auth/register', {
      email,
      password,
      fullName,
    })
    applyAuth(data)
  }

  const logout = async () => {
    const refreshToken = tokenStorage.getRefreshToken()
    if (refreshToken) {
      await api.post('/api/auth/logout', { refreshToken }).catch(() => {})
    }
    tokenStorage.clear()
    setUser(null)
  }

  return (
    <AuthContext.Provider value={{ user, isInitializing, login, register, logout }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside <AuthProvider>')
  return ctx
}
