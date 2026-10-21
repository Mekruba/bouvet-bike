import type { ReactNode } from 'react'
import { Link } from 'react-router'

interface PageHeaderProps {
  title: ReactNode
  subtitle?: ReactNode
  back?: { to: string; label: string }
  actions?: ReactNode
}

export function PageHeader({ title, subtitle, back, actions }: PageHeaderProps) {
  return (
    <header className="page-header">
      <div>
        {back && (
          <Link className="page-header__back" to={back.to}>
            ← {back.label}
          </Link>
        )}
        <h1>{title}</h1>
        {subtitle && <p className="page-header__subtitle">{subtitle}</p>}
      </div>
      {actions && <div className="page-header__actions">{actions}</div>}
    </header>
  )
}
