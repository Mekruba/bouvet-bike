import { useCallback, useState } from 'react'
import type { ChangeEvent } from 'react'
import { ApiError, errorMessage } from '../api/client.ts'

/**
 * Form state for controlled inputs, plus the error handling every form shares:
 * field errors from a 400/422 response go next to the fields, anything else becomes the form error.
 */
export function useForm<T extends Record<string, unknown>>(initial: T) {
  const [values, setValues] = useState<T>(initial)
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  /** Props for an input bound to `name`: `<TextField {...bind('email')} />`. */
  const bind = (name: keyof T & string) => ({
    name,
    value: (values[name] ?? '') as string | number,
    error: fieldErrors[name],
    onChange: (event: ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) =>
      setValues((current) => ({ ...current, [name]: event.target.value })),
  })

  const set = useCallback(<K extends keyof T>(name: K, value: T[K]) => setValues((current) => ({ ...current, [name]: value })), [])

  /** Runs `action`, tracking `submitting` and turning a failure into field or form errors. Returns undefined on failure. */
  const submit = async <R,>(action: () => Promise<R>, onError?: (error: unknown) => void): Promise<R | undefined> => {
    setSubmitting(true)
    setFieldErrors({})
    setFormError(null)
    try {
      return await action()
    } catch (error) {
      const fields = error instanceof ApiError ? error.fieldErrors : {}
      setFieldErrors(fields)
      const unmatched = Object.keys(fields).filter((key) => !(key in values))
      setFormError(Object.keys(fields).length > 0 && unmatched.length === 0 ? null : errorMessage(error))
      onError?.(error)
      return undefined
    } finally {
      setSubmitting(false)
    }
  }

  return { values, setValues, set, bind, fieldErrors, formError, setFormError, submitting, submit }
}
