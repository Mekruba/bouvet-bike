import { useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router'
import { ApiError, errorMessage } from '../api/client.ts'
import { ordersApi } from '../api/endpoints.ts'
import type { CustomerOrder, OrderStatus } from '../api/types.ts'
import { useApi } from '../api/useApi.ts'
import { Alert } from '../components/Alert.tsx'
import { Button } from '../components/Button.tsx'
import { Card } from '../components/Card.tsx'
import { Loading } from '../components/Feedback.tsx'
import { TextAreaField } from '../components/Field.tsx'
import { PageHeader } from '../components/PageHeader.tsx'
import { PriceSummary } from '../components/PriceSummary.tsx'
import { StatusBadge } from '../components/StatusBadge.tsx'
import { cx } from '../lib/cx.ts'
import { formatDate, formatDateTime } from '../lib/format.ts'

/** An order as the customer sees it. The reference in the URL is all it takes. */
export function OrderPage() {
  const { reference = '' } = useParams()
  const [searchParams] = useSearchParams()
  const order = useApi(() => ordersApi.get(reference), [reference])

  if (order.error instanceof ApiError && order.error.status === 404) {
    return (
      <Alert tone="warning" title={`No order with the reference `} action={<Link to="/orders/find">Find my order</Link>}>
        Check the reference in your confirmation, or search with the email or phone number you booked with.
      </Alert>
    )
  }
  if (order.error) return <Alert tone="danger" title="Could not load the order">{errorMessage(order.error)}</Alert>
  if (!order.data) return <Loading />

  return <OrderDetails order={order.data} placed={searchParams.has('placed')} onChange={order.setData} />
}

function OrderDetails({
  order,
  placed,
  onChange,
}: {
  order: CustomerOrder
  placed: boolean
  onChange: (order: CustomerOrder) => void
}) {
  return (
    <div className="stack stack--lg">
      {placed && (
        <Alert tone="success" title="Thank you, your booking is confirmed">
          Your reference is <strong className="mono">{order.reference}</strong>. Use it, or the email or phone number you
          booked with, to follow the repair under Find my order.
        </Alert>
      )}

      <PageHeader
        title={
          <span className="cluster">
            Order <span className="mono">{order.reference}</span>
          </span>
        }
        subtitle={`Booked ${formatDateTime(order.createdAt)}`}
        actions={<StatusBadge status={order.status} />}
      />

      <div className="with-sidebar">
        <div className="stack stack--lg">
          <Card title="Progress">
            <StatusProgress status={order.status} />
          </Card>

          <Card title="Your booking">
            <dl className="details">
              <dt>Service</dt>
              <dd>{order.serviceName}</dd>
              <dt>Bike</dt>
              <dd>{[order.bikeBrand, order.bikeModel].filter(Boolean).join(' ')}</dd>
              <dt>Due date</dt>
              <dd>{formatDate(order.dueDate)}</dd>
              <dt>Name</dt>
              <dd>{order.contact.name}</dd>
              <dt>Email</dt>
              <dd>{order.contact.email}</dd>
              <dt>Phone</dt>
              <dd>{order.contact.phone}</dd>
            </dl>
          </Card>

          {/* Keyed on the saved notes, so the editor resets when they change. */}
          <NotesCard key={order.notes ?? ''} order={order} onChange={onChange} />
        </div>

        <aside className="with-sidebar__aside">
          <Card title="Price">
            <PriceSummary price={order.price} serviceLabel={order.serviceName} totalLabel="Estimated total" />
          </Card>
        </aside>
      </div>
    </div>
  )
}

const steps: Array<{ status: OrderStatus; label: string }> = [
  { status: 'New', label: 'Booked' },
  { status: 'InProgress', label: 'In the workshop' },
  { status: 'Completed', label: 'Ready for pickup' },
]

function StatusProgress({ status }: { status: OrderStatus }) {
  if (status === 'Cancelled') {
    return <Alert tone="warning" title="This order was cancelled">Contact us if you think this is a mistake.</Alert>
  }

  const current = status === 'WaitingForCustomer' ? 1 : steps.findIndex((step) => step.status === status)
  return (
    <div className="stack">
      <div className="progress">
        {steps.map((step, index) => (
          <div
            key={step.status}
            className={cx('progress__step', index < current && 'is-done', index === current && (status === 'Completed' ? 'is-done' : 'is-current'))}
          >
            <span className="progress__bar" />
            {step.label}
          </div>
        ))}
      </div>
      {status === 'WaitingForCustomer' && (
        <Alert tone="info" title="We need to hear from you">
          The repair is paused until we've talked. We'll contact you, or reach out to us with your reference.
        </Alert>
      )}
    </div>
  )
}

function NotesCard({ order, onChange }: { order: CustomerOrder; onChange: (order: CustomerOrder) => void }) {
  const [notes, setNotes] = useState(order.notes ?? '')
  const [saving, setSaving] = useState(false)
  const [message, setMessage] = useState<{ tone: 'success' | 'danger'; text: string } | null>(null)

  if (!order.canEditNotes) {
    return (
      <Card title="Notes for the workshop">
        <div className="stack stack--sm">
          {order.notes ? <p>{order.notes}</p> : <p className="muted">No notes.</p>}
          <p className="small muted">Notes can't be changed once the repair has started.</p>
        </div>
      </Card>
    )
  }

  const save = async () => {
    setSaving(true)
    setMessage(null)
    try {
      const trimmed = notes.trim()
      const updated = await ordersApi.patch(
        order.reference,
        trimmed ? [{ op: 'replace', path: '/notes', value: trimmed }] : [{ op: 'remove', path: '/notes' }],
      )
      onChange(updated)
      setMessage({ tone: 'success', text: 'Notes saved.' })
    } catch (error) {
      setMessage({ tone: 'danger', text: errorMessage(error) })
    } finally {
      setSaving(false)
    }
  }

  return (
    <Card title="Notes for the workshop" description="You can change these until we start the repair.">
      <div className="stack">
        <TextAreaField label="Notes" value={notes} onChange={(event) => setNotes(event.target.value)} rows={4} />
        {message && <Alert tone={message.tone}>{message.text}</Alert>}
        <div className="form-actions">
          <Button variant="secondary" disabled={notes === (order.notes ?? '')} loading={saving} onClick={save}>
            Save notes
          </Button>
        </div>
      </div>
    </Card>
  )
}
