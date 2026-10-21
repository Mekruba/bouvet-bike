import type { JsonPatchOperation } from './types.ts'

/** RFC 7807 problem details, as returned by the API. Extra fields (e.g. `nextAvailableDate`) are kept. */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  errors?: Record<string, string[]>
  [extension: string]: unknown
}

export class ApiError extends Error {
  readonly status: number
  readonly problem?: ProblemDetails

  constructor(status: number, problem?: ProblemDetails) {
    super(problem?.detail ?? problem?.title ?? `Request failed with status ${status}`)
    this.status = status
    this.problem = problem
  }

  /** Field errors keyed by camelCase field name ("Email" and "email" both become "email"). */
  get fieldErrors(): Record<string, string> {
    const result: Record<string, string> = {}
    for (const [key, messages] of Object.entries(this.problem?.errors ?? {})) {
      const field = key.length > 0 ? key[0].toLowerCase() + key.slice(1) : key
      result[field] = messages.join(' ')
    }
    return result
  }
}

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'
  /** Sent as JSON. */
  body?: unknown
  /** Sent as an RFC 6902 JSON Patch document. */
  patch?: JsonPatchOperation[]
  headers?: Record<string, string>
}

/** Calls the API. Relative `/api` paths go through the Vite proxy, so the auth cookie is same-origin. */
export async function api<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json', ...options.headers }
  let body: string | undefined
  if (options.patch) {
    headers['Content-Type'] = 'application/json-patch+json'
    body = JSON.stringify(options.patch)
  } else if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json'
    body = JSON.stringify(options.body)
  }

  const response = await fetch(`/api${path}`, {
    method: options.method ?? (options.patch ? 'PATCH' : body ? 'POST' : 'GET'),
    headers,
    body,
    credentials: 'same-origin',
  })

  if (!response.ok) {
    const problem = response.headers.get('Content-Type')?.includes('json')
      ? ((await response.json()) as ProblemDetails)
      : undefined
    throw new ApiError(response.status, problem)
  }

  return response.status === 204 ? (undefined as T) : ((await response.json()) as T)
}

/** Builds a query string from defined values only. */
export function query(params: Record<string, string | number | boolean | undefined | null>): string {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== '') {
      search.set(key, String(value))
    }
  }
  const text = search.toString()
  return text ? `?${text}` : ''
}

/** A user-facing message for any error thrown by `api`. */
export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    return error.problem?.detail ?? error.problem?.title ?? error.message
  }
  return error instanceof Error ? error.message : 'Something went wrong.'
}
