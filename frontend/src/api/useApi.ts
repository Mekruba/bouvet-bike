import { useCallback, useEffect, useState } from 'react'
import type { DependencyList } from 'react'

interface ApiState<T> {
  data: T | undefined
  error: unknown
  loading: boolean
}

/**
 * Loads data when the component mounts and whenever `deps` change. `reload()` fetches again;
 * `setData()` replaces the data with a fresh response (e.g. from a PATCH) without refetching.
 */
export function useApi<T>(load: () => Promise<T>, deps: DependencyList) {
  const [state, setState] = useState<ApiState<T>>({ data: undefined, error: undefined, loading: true })
  const [version, setVersion] = useState(0)

  useEffect(() => {
    let active = true
    setState((current) => ({ ...current, loading: true }))
    load().then(
      (data) => active && setState({ data, error: undefined, loading: false }),
      (error: unknown) => active && setState((current) => ({ ...current, error, loading: false })),
    )
    return () => {
      active = false
    }
    // `load` is recreated every render; `deps` says when it actually changes.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, version])

  const reload = useCallback(() => setVersion((v) => v + 1), [])
  const setData = useCallback((data: T) => setState({ data, error: undefined, loading: false }), [])

  return { ...state, reload, setData }
}
