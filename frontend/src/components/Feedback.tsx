import type { ReactNode } from 'react'
import { errorMessage } from '../api/client.ts'
import { Alert } from './Alert.tsx'

export function Loading({ label = 'Loading…' }: { label?: string }) {
  return (
    <div className="loading">
      <span className="spinner" aria-hidden />
      {label}
    </div>
  )
}

export function EmptyState({ title, children }: { title: ReactNode; children?: ReactNode }) {
  return (
    <div className="empty">
      <p className="empty__title">{title}</p>
      {children && <p className="small">{children}</p>}
    </div>
  )
}

/** Loading spinner, error alert, or the content: the three states of data loaded with useApi. */
export function AsyncContent<T>({
  data,
  error,
  loading,
  children,
}: {
  data: T | undefined
  error: unknown
  loading: boolean
  children: (data: T) => ReactNode
}) {
  if (data !== undefined) return <>{children(data)}</>
  if (error) return <Alert tone="danger" title="Could not load">{errorMessage(error)}</Alert>
  return loading ? <Loading /> : null
}
