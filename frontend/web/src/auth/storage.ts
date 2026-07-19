import type { User } from './types'

const ACCESS_KEY = 'lares.access'
const REFRESH_KEY = 'lares.refresh'
const USER_KEY = 'lares.user'

export const tokenStorage = {
  getAccessToken: () => localStorage.getItem(ACCESS_KEY),
  getRefreshToken: () => localStorage.getItem(REFRESH_KEY),
  getUser: (): User | null => {
    const raw = localStorage.getItem(USER_KEY)
    return raw ? (JSON.parse(raw) as User) : null
  },
  save: (accessToken: string, refreshToken: string, user: User) => {
    localStorage.setItem(ACCESS_KEY, accessToken)
    localStorage.setItem(REFRESH_KEY, refreshToken)
    localStorage.setItem(USER_KEY, JSON.stringify(user))
  },
  clear: () => {
    localStorage.removeItem(ACCESS_KEY)
    localStorage.removeItem(REFRESH_KEY)
    localStorage.removeItem(USER_KEY)
  },
}
