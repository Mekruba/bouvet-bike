import { useNavigate } from 'react-router'
import { ordersApi } from '../api/endpoints.ts'
import type { CustomerOrderSummary } from '../api/types.ts'
import { useApi } from '../api/useApi.ts'
import { ButtonLink } from '../components/Button.tsx'
import { Card } from '../components/Card.tsx'
import { DataTable } from '../components/DataTable.tsx'
import type { Column } from '../components/DataTable.tsx'
import { Alert } from '../components/Alert.tsx'
import { EmptyState } from '../components/Feedback.tsx'
import { PageHeader } from '../components/PageHeader.tsx'
import { StatusBadge } from '../components/StatusBadge.tsx'
import { errorMessage } from '../api/client.ts'
import { formatDate, formatMoney } from '../lib/format.ts'

const columns: Column<CustomerOrderSummary>[] = [
  { key: 'reference', header: 'Reference', render: (o) => <span className="mono">{o.reference}</span> },
  { key: 'service', header: 'Service', render: (o) => <span className="table__primary">{o.serviceName}</span> },
  { key: 'bike', header: 'Bike', render: (o) => o.bikeBrand },
  { key: 'due', header: 'Due date', render: (o) => formatDate(o.dueDate) },
  { key: 'status', header: 'Status', render: (o) => <StatusBadge status={o.status} /> },
  { key: 'total', header: 'Total', align: 'right', render: (o) => formatMoney(o.total) },
]

/** A logged-in customer's orders. Orders placed as a guest aren't linked to the account. */
export function MyOrdersPage() {
  const navigate = useNavigate()
  const orders = useApi(() => ordersApi.mine(), [])

  return (
    <>
      <PageHeader title="My orders" subtitle="Orders placed while logged in." actions={<ButtonLink to="/">Book a service</ButtonLink>} />
      {orders.error ? (
        <Alert tone="danger">{errorMessage(orders.error)}</Alert>
      ) : (
        <Card flush>
          <DataTable
            columns={columns}
            rows={orders.data}
            loading={orders.loading}
            rowKey={(o) => o.reference}
            onRowClick={(o) => navigate(`/orders/${o.reference}`)}
            empty={<EmptyState title="No orders yet">Book your first service and it will show up here.</EmptyState>}
          />
        </Card>
      )}
    </>
  )
}
