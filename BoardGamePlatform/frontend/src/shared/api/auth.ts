export interface User {
  id: string
  email: string
  displayName: string
}

export interface AuthResponse {
  accessToken: string
  refreshToken: string
  user: User
}

export interface RegisterRequest {
  email: string
  password: string
  displayName: string
}

export interface LoginRequest {
  identifier: string
  password: string
}

export interface RefreshTokenRequest {
  refreshToken: string
}

export interface LogoutRequest {
  refreshToken: string
}

export interface UpdateProfileRequest {
  displayName: string
}

export interface ChangePasswordRequest {
  currentPassword: string
  newPassword: string
}