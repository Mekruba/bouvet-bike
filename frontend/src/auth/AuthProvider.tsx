import { useCallback, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { accountApi } from '../api/endpoints.ts'
import type { CurrentUser, Role } from '../api/types.ts'
import { AuthContext } from './authContext.ts'

/** Holds the signed-in user. The session itself is an HttpOnly cookie the browser sends automatically. */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    accountApi
      .me()
      .then(setUser, () => setUser(null))
      .finally(() => setLoading(false))
  }, [])

  const login = useCallback(async (email: string, password: string, rememberMe: boolean) => {
    const signedIn = await accountApi.login(email, password, rememberMe)
    setUser(signedIn)
    return signedIn
  }, [])

  // Not signed in yet: the email has to be confirmed first.
  const register = useCallback(
    (name: string, email: string, phone: string | null, password: string) => accountApi.register(name, email, phone, password),
    [],
  )

  const logout = useCallback(async () => {
    await accountApi.logout()
    setUser(null)
  }, [])

  const value = useMemo(
    () => ({
      user,
      loading,
      hasRole: (role: Role) => user?.roles.includes(role) ?? false,
      login,
      register,
      logout,
    }),
    [user, loading, login, register, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
