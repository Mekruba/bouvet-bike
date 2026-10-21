import { createContext } from 'react'
import type { CurrentUser, Role } from '../api/types.ts'

export interface AuthState {
  /** Null when logged out. */
  user: CurrentUser | null
  /** True until the first "who am I" check has finished. */
  loading: boolean
  hasRole: (role: Role) => boolean
  login: (email: string, password: string, rememberMe: boolean) => Promise<CurrentUser>
  register: (name: string, email: string, phone: string | null, password: string) => Promise<CurrentUser>
  logout: () => Promise<void>
}

export const AuthContext = createContext<AuthState | null>(null)
