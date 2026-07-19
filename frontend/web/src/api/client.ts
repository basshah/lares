import axios from 'axios'
import { tokenStorage } from '../auth/storage'
import type { AuthResponse } from '../auth/types'

export const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL,
})

api.interceptors.request.use((config) => {
  const token = tokenStorage.getAccessToken()
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

let refreshPromise: Promise<string> | null = null

async function refreshAccessToken(): Promise<string> {
  const refreshToken = tokenStorage.getRefreshToken()
  if (!refreshToken) throw new Error('No refresh token')

  // Bare axios call: going through `api` would loop back into this interceptor.
  const { data } = await axios.post<AuthResponse>(
    `${import.meta.env.VITE_API_URL}/api/auth/refresh`,
    { refreshToken },
  )
  tokenStorage.save(data.accessToken, data.refreshToken, data.user)
  return data.accessToken
}

api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const original = error.config
    const isAuthCall = original?.url?.includes('/api/auth/')

    if (error.response?.status === 401 && !original._retried && !isAuthCall) {
      original._retried = true
      try {
        // Share one in-flight refresh between concurrent 401s.
        refreshPromise ??= refreshAccessToken().finally(() => {
          refreshPromise = null
        })
        const newToken = await refreshPromise
        original.headers.Authorization = `Bearer ${newToken}`
        return api(original)
      } catch {
        tokenStorage.clear()
        window.location.href = '/login'
      }
    }

    return Promise.reject(error)
  },
)
