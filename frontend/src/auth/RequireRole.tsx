import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router'
import type { Role } from '../api/types.ts'
import { Alert } from '../components/Alert.tsx'
import { Loading } from '../components/Feedback.tsx'
import { useAuth } from './useAuth.ts'

/**
 * Shows the page only to users with one of the roles; anonymous users go to the login page and come back.
 * This is for the UI only: the API enforces the same rules on every request.
 */
export function RequireRole({ roles, children }: { roles: Role[]; children: ReactNode }) {
  const { user, loading, hasRole } = useAuth()
  const location = useLocation()

  if (loading) return <Loading />
  if (!user) {
    const returnTo = encodeURIComponent(location.pathname + location.search)
    return <Navigate to={`/login?returnTo=${returnTo}`} replace />
  }
  if (!roles.some(hasRole)) {
    return (
      <Alert tone="warning" title="No access">
        This page is for {roles.join(' or ').toLowerCase()} accounts. You are logged in as {user.email}.
      </Alert>
    )
  }
  return <>{children}</>
}
