/** RFC 7807 problem details, as returned by ASP.NET Core (validation errors include `errors`). */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  readonly status: number
  readonly problem?: ProblemDetails

  constructor(status: number, problem?: ProblemDetails) {
    super(problem?.detail ?? problem?.title ?? `Request failed with status ${status}`)
    this.status = status
    this.problem = problem
  }
}

export async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`/api${path}`, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...init?.headers },
  })

  if (!response.ok) {
    const problem = response.headers.get('Content-Type')?.includes('json')
      ? ((await response.json()) as ProblemDetails)
      : undefined
    throw new ApiError(response.status, problem)
  }

  return response.status === 204 ? (undefined as T) : ((await response.json()) as T)
}
