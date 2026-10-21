import type { ReactNode } from 'react'
import { cx } from '../lib/cx.ts'

interface CardProps {
  title?: ReactNode
  description?: ReactNode
  /** Shows a numbered step marker before the title (booking form). */
  step?: number
  actions?: ReactNode
  footer?: ReactNode
  /** No body padding, for tables. */
  flush?: boolean
  className?: string
  children?: ReactNode
}

export function Card({ title, description, step, actions, footer, flush, className, children }: CardProps) {
  return (
    <section className={cx('card', className)}>
      {(title || actions) && (
        <header className="card__header">
          <div>
            {title && (
              <h2 className="card__title">
                {step !== undefined && <span className="card__step">{step}</span>}
                {title}
              </h2>
            )}
            {description && <p className="card__description">{description}</p>}
          </div>
          {actions && <div className="cluster">{actions}</div>}
        </header>
      )}
      {children !== undefined && <div className={cx('card__body', flush && 'card__body--flush')}>{children}</div>}
      {footer && <footer className="card__footer">{footer}</footer>}
    </section>
  )
}
