import type { ReactNode } from 'react'
import { useNavigate } from 'react-router'
import type { CustomerOrderSummary } from '../api/types.ts'
import { formatDate, formatMoney } from '../lib/format.ts'
import { DataTable } from './DataTable.tsx'
import type { Column } from './DataTable.tsx'
import { StatusBadge } from './StatusBadge.tsx'

const columns: Column<CustomerOrderSummary>[] = [
  { key: 'reference', header: 'Reference', render: (o) => <span className="mono">{o.reference}</span> },
  { key: 'service', header: 'Service', render: (o) => <span className="table__primary">{o.serviceName}</span> },
  { key: 'bike', header: 'Bike', render: (o) => o.bikeBrand },
  { key: 'due', header: 'Due date', render: (o) => formatDate(o.dueDate) },
  { key: 'status', header: 'Status', render: (o) => <StatusBadge status={o.status} /> },
  { key: 'total', header: 'Total', align: 'right', render: (o) => formatMoney(o.total) },
]

/** A customer's orders, newest first; a row opens the order. Used for "My orders" and guest lookup. */
export function OrderSummaryTable({ orders, loading, empty }: { orders: CustomerOrderSummary[] | undefined; loading?: boolean; empty: ReactNode }) {
  const navigate = useNavigate()
  return (
    <DataTable
      columns={columns}
      rows={orders}
      loading={loading}
      rowKey={(o) => o.reference}
      onRowClick={(o) => navigate(`/orders/${o.reference}`)}
      empty={empty}
    />
  )
}
