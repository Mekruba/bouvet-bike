import type { ButtonHTMLAttributes, ReactNode } from 'react'
import { Link } from 'react-router'
import type { LinkProps } from 'react-router'
import { cx } from '../lib/cx.ts'

type Variant = 'primary' | 'secondary' | 'danger' | 'ghost'
type Size = 'sm' | 'md' | 'lg'

interface StyleProps {
  variant?: Variant
  size?: Size
  block?: boolean
}

const classes = ({ variant = 'primary', size = 'md', block }: StyleProps, className?: string) =>
  cx('btn', `btn--${variant}`, size !== 'md' && `btn--${size}`, block && 'btn--block', className)

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement>, StyleProps {
  loading?: boolean
  children: ReactNode
}

export function Button({ variant, size, block, loading, className, disabled, type = 'button', children, ...rest }: ButtonProps) {
  return (
    <button type={type} className={classes({ variant, size, block }, className)} disabled={disabled || loading} {...rest}>
      {loading && <span className="spinner spinner--inline" aria-hidden />}
      {children}
    </button>
  )
}

/** A router link that looks like a button. */
export function ButtonLink({ variant, size, block, className, ...rest }: LinkProps & StyleProps) {
  return <Link className={classes({ variant, size, block }, className)} {...rest} />
}
