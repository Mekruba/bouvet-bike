import { useId } from 'react'
import type { InputHTMLAttributes, ReactNode, SelectHTMLAttributes, TextareaHTMLAttributes } from 'react'
import { cx } from '../lib/cx.ts'

interface FieldProps {
  label: ReactNode
  /** Marks the field "(optional)". */
  optional?: boolean
  hint?: ReactNode
  error?: string
  className?: string
  children: (id: string, describedBy: string | undefined) => ReactNode
}

/** Label, control, hint and error for one form field. The concrete fields below use it. */
export function Field({ label, optional, hint, error, className, children }: FieldProps) {
  const id = useId()
  const hintId = hint ? `${id}-hint` : undefined
  const errorId = error ? `${id}-error` : undefined
  const describedBy = [hintId, errorId].filter(Boolean).join(' ') || undefined

  return (
    <div className={cx('field', error && 'field--invalid', className)}>
      <label className="field__label" htmlFor={id}>
        {label}
        {optional && <span className="field__optional">(optional)</span>}
      </label>
      {children(id, describedBy)}
      {hint && !error && (
        <span id={hintId} className="field__hint">
          {hint}
        </span>
      )}
      {error && (
        <span id={errorId} className="field__error" role="alert">
          {error}
        </span>
      )}
    </div>
  )
}

type Common = Pick<FieldProps, 'label' | 'optional' | 'hint' | 'error' | 'className'>

export function TextField({ label, optional, hint, error, className, ...input }: Common & InputHTMLAttributes<HTMLInputElement>) {
  return (
    <Field label={label} optional={optional} hint={hint} error={error} className={className}>
      {(id, describedBy) => (
        <input id={id} className="input" aria-invalid={!!error} aria-describedby={describedBy} {...input} />
      )}
    </Field>
  )
}

export interface SelectOption {
  value: string
  label: string
}

export function SelectField({
  label,
  optional,
  hint,
  error,
  className,
  options,
  placeholder,
  ...select
}: Common & SelectHTMLAttributes<HTMLSelectElement> & { options: SelectOption[]; placeholder?: string }) {
  return (
    <Field label={label} optional={optional} hint={hint} error={error} className={className}>
      {(id, describedBy) => (
        <select id={id} className="select" aria-invalid={!!error} aria-describedby={describedBy} {...select}>
          {placeholder !== undefined && <option value="">{placeholder}</option>}
          {options.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </select>
      )}
    </Field>
  )
}

export function TextAreaField({ label, optional, hint, error, className, ...textarea }: Common & TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return (
    <Field label={label} optional={optional} hint={hint} error={error} className={className}>
      {(id, describedBy) => (
        <textarea id={id} className="textarea" aria-invalid={!!error} aria-describedby={describedBy} {...textarea} />
      )}
    </Field>
  )
}

export function Checkbox({ label, ...input }: { label: ReactNode } & InputHTMLAttributes<HTMLInputElement>) {
  return (
    <label className="checkbox">
      <input type="checkbox" {...input} />
      {label}
    </label>
  )
}
