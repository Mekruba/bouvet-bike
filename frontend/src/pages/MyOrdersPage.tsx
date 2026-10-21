import { errorMessage } from '../api/client.ts'
import { ordersApi } from '../api/endpoints.ts'
import { useApi } from '../api/useApi.ts'
import { Alert } from '../components/Alert.tsx'
import { ButtonLink } from '../components/Button.tsx'
import { Card } from '../components/Card.tsx'
import { EmptyState } from '../components/Feedback.tsx'
import { OrderSummaryTable } from '../components/OrderSummaryTable.tsx'
import { PageHeader } from '../components/PageHeader.tsx'

/** A logged-in customer's orders. Orders placed as a guest are found under "Find my order". */
export function MyOrdersPage() {
  const orders = useApi(() => ordersApi.mine(), [])

  return (
    <>
      <PageHeader title="My orders" subtitle="Orders placed while logged in." actions={<ButtonLink to="/">Book a service</ButtonLink>} />
      {orders.error ? (
        <Alert tone="danger">{errorMessage(orders.error)}</Alert>
      ) : (
        <Card flush>
          <OrderSummaryTable
            orders={orders.data}
            loading={orders.loading}
            empty={<EmptyState title="No orders yet">Book your first service and it will show up here.</EmptyState>}
          />
        </Card>
      )}
    </>
  )
}
