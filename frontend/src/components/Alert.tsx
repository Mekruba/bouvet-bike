import type { ReactNode } from 'react'

type Tone = 'info' | 'success' | 'warning' | 'danger'

const icons: Record<Tone, ReactNode> = {
  info: <path d="M10 18a8 8 0 1 0 0-16 8 8 0 0 0 0 16Zm0-11.5v.01M10 9v5" />,
  success: <path d="M10 18a8 8 0 1 0 0-16 8 8 0 0 0 0 16Zm-3.5-8 2.5 2.5 4.5-5" />,
  warning: <path d="M10 3 2 17h16L10 3Zm0 5v4m0 2.5v.01" />,
  danger: <path d="M10 18a8 8 0 1 0 0-16 8 8 0 0 0 0 16Zm0-12v5m0 3v.01" />,
}

interface AlertProps {
  tone?: Tone
  title?: ReactNode
  action?: ReactNode
  children?: ReactNode
}

export function Alert({ tone = 'info', title, action, children }: AlertProps) {
  return (
    <div className={`alert alert--${tone}`} role={tone === 'danger' ? 'alert' : 'status'}>
      <svg className="alert__icon" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden>
        {icons[tone]}
      </svg>
      <div className="alert__content">
        {title && <p className="alert__title">{title}</p>}
        {children && <div>{children}</div>}
        {action && <div className="alert__action">{action}</div>}
      </div>
    </div>
  )
}
