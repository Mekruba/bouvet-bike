import type { OrderStatus } from '../api/types.ts'
import { statusLabels } from '../lib/format.ts'

export function StatusBadge({ status }: { status: OrderStatus }) {
  return <span className={`badge badge--dot badge--status-${status.toLowerCase()}`}>{statusLabels[status]}</span>
}
