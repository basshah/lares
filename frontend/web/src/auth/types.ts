export interface User {
  id: string
  email: string
  fullName: string
  roles: string[]
}

export interface AuthResponse {
  accessToken: string
  accessTokenExpiresAtUtc: string
  refreshToken: string
  user: User
}

export interface ApiError {
  code: string
}
