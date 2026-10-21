import { useState } from 'react'
import type { FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router'
import { ApiError, errorMessage } from '../../api/client.ts'
import { adminOrdersApi, catalogApi } from '../../api/endpoints.ts'
import type { AdminOrder, OrderStatus, ServiceType } from '../../api/types.ts'
import { useApi } from '../../api/useApi.ts'
import { useAuth } from '../../auth/useAuth.ts'
import { Alert } from '../../components/Alert.tsx'
import { Button } from '../../components/Button.tsx'
import { Card } from '../../components/Card.tsx'
import { ConfirmDialog } from '../../components/Dialog.tsx'
import { AsyncContent } from '../../components/Feedback.tsx'
import { SelectField, TextAreaField, TextField } from '../../components/Field.tsx'
import { PageHeader } from '../../components/PageHeader.tsx'
import { PriceSummary } from '../../components/PriceSummary.tsx'
import { StatusBadge } from '../../components/StatusBadge.tsx'
import { formatDate, formatDateTime, formatMoney, statusLabels } from '../../lib/format.ts'
import { diffPatch } from '../../lib/jsonPatch.ts'
import { useForm } from '../../lib/useForm.ts'

const isClosed = (status: OrderStatus) => status === 'Completed' || status === 'Cancelled'

/** One order for staff: edit details, move it through its lifecycle, adjust the price. */
export function AdminOrderPage() {
  const { id = '' } = useParams()
  const order = useApi(() => adminOrdersApi.get(id), [id])
  const services = useApi(() => catalogApi.list(true), [])

  return (
    <AsyncContent {...order}>
      {(data) => <OrderView order={data} services={services.data ?? []} onChange={order.setData} reload={order.reload} />}
    </AsyncContent>
  )
}

interface SectionProps {
  order: AdminOrder
  onChange: (order: AdminOrder) => void
  reload: () => void
}

function OrderView({ order, services, onChange, reload }: SectionProps & { services: ServiceType[] }) {
  const { hasRole } = useAuth()
  const navigate = useNavigate()
  const [confirmDelete, setConfirmDelete] = useState(false)
  const [deleting, setDeleting] = useState(false)
  const [deleteError, setDeleteError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  // Sections remount after a save (they're keyed on updatedAt), so the confirmation lives here.
  const saved = (updated: AdminOrder) => {
    onChange(updated)
    setNotice(`Saved at ${formatDateTime(updated.updatedAt)}.`)
  }

  const remove = async () => {
    setDeleting(true)
    try {
      await adminOrdersApi.remove(order.id)
      navigate('/admin/orders', { replace: true })
    } catch (error) {
      setDeleteError(errorMessage(error))
      setConfirmDelete(false)
    } finally {
      setDeleting(false)
    }
  }

  return (
    <div className="stack stack--lg">
      <PageHeader
        back={{ to: '/admin/orders', label: 'Orders' }}
        title={
          <span className="cluster">
            <span className="mono">{order.reference}</span>
            <StatusBadge status={order.status} />
          </span>
        }
        subtitle={`Booked ${formatDateTime(order.createdAt)} · ${order.customerId ? 'customer account' : 'guest'} · last changed ${formatDateTime(order.updatedAt)}`}
        actions={
          hasRole('Admin') && (
            <Button variant="secondary" onClick={() => setConfirmDelete(true)}>
              Delete order
            </Button>
          )
        }
      />
      {deleteError && <Alert tone="danger">{deleteError}</Alert>}
      {notice && <Alert tone="success">{notice}</Alert>}

      <div className="with-sidebar">
        <div className="stack stack--lg">
          {/* Keyed on the last change, so the forms reset to the saved order after every update. */}
          <DetailsCard key={order.updatedAt} order={order} services={services} onChange={saved} reload={reload} />
          <NotesCard key={`notes-${order.updatedAt}`} order={order} onChange={saved} reload={reload} />
        </div>
        <aside className="stack stack--lg">
          <StatusCard order={order} onChange={onChange} reload={reload} />
          <PriceCard order={order} onChange={saved} reload={reload} />
        </aside>
      </div>

      <ConfirmDialog
        open={confirmDelete}
        title={`Delete ${order.reference}?`}
        message="The order and its history are removed for good. To keep the history, cancel the order instead."
        confirmLabel="Delete order"
        tone="danger"
        busy={deleting}
        onConfirm={remove}
        onCancel={() => setConfirmDelete(false)}
      />
    </div>
  )
}

// ---- Details: JSON Patch with only the changed fields ----

function detailsOf(order: AdminOrder) {
  return {
    name: order.contact.name,
    email: order.contact.email,
    phone: order.contact.phone,
    bikeBrand: order.bikeBrand,
    bikeModel: order.bikeModel ?? '',
    serviceTypeId: order.serviceTypeId,
    dueDate: order.dueDate,
  }
}

function DetailsCard({ order, services, onChange }: SectionProps & { services: ServiceType[] }) {
  const form = useForm(detailsOf(order))
  const [nextAvailable, setNextAvailable] = useState<string | null>(null)
  const closed = isClosed(order.status)
  const { setValues } = form

  const patch = diffPatch(detailsOf(order), form.values, ['name', 'email', 'phone', 'bikeBrand', 'bikeModel', 'serviceTypeId', 'dueDate'], ['bikeModel'])

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    setNextAvailable(null)
    const updated = await form.submit(
      () => adminOrdersApi.patch(order.id, patch),
      (error) => {
        if (error instanceof ApiError && typeof error.problem?.nextAvailableDate === 'string') setNextAvailable(error.problem.nextAvailableDate)
      },
    )
    if (updated) onChange(updated)
  }

  // An inactive service can't be chosen, but the order's current one is always listed.
  const serviceOptions = services
    .filter((s) => s.isActive || s.id === order.serviceTypeId)
    .map((s) => ({ value: s.id, label: `${s.name} (${formatMoney(s.price)})${s.isActive ? '' : ', retired'}` }))

  return (
    <Card title="Order details" description={closed ? `A ${statusLabels[order.status].toLowerCase()} order can't be edited.` : undefined}>
      <form className="stack" onSubmit={handleSubmit} noValidate>
        <fieldset className="fieldset grid-2" disabled={closed}>
          <TextField className="span-2" label="Customer name" {...form.bind('name')} />
          <TextField label="Email" type="email" {...form.bind('email')} />
          <TextField label="Phone" type="tel" {...form.bind('phone')} />
          <TextField label="Bike brand" {...form.bind('bikeBrand')} />
          <TextField label="Bike model" optional {...form.bind('bikeModel')} />
          <SelectField
            label="Service"
            options={serviceOptions}
            hint="Changing the service prices it from the catalog now."
            {...form.bind('serviceTypeId')}
          />
          <TextField label="Due date" type="date" hint="Checked against capacity." {...form.bind('dueDate')} />
        </fieldset>
        {nextAvailable && (
          <Alert
            tone="warning"
            title="That day is fully booked"
            action={
              <Button size="sm" variant="secondary" onClick={() => form.set('dueDate', nextAvailable)}>
                Use {formatDate(nextAvailable)}
              </Button>
            }
          />
        )}
        {form.formError && !nextAvailable && <Alert tone="danger">{form.formError}</Alert>}
        {!closed && (
          <div className="form-actions">
            <Button variant="ghost" disabled={patch.length === 0} onClick={() => setValues(detailsOf(order))}>
              Reset
            </Button>
            <Button type="submit" disabled={patch.length === 0} loading={form.submitting}>
              Save changes
            </Button>
          </div>
        )}
      </form>
    </Card>
  )
}

// ---- Notes ----

function NotesCard({ order, onChange }: SectionProps) {
  const [notes, setNotes] = useState(order.internalNotes ?? '')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const save = async () => {
    setSaving(true)
    setError(null)
    try {
      onChange(await adminOrdersApi.patch(order.id, diffPatch({ internalNotes: order.internalNotes ?? '' }, { internalNotes: notes.trim() }, ['internalNotes'], ['internalNotes'])))
    } catch (e) {
      setError(errorMessage(e))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Card title="Notes">
      <div className="stack">
        <div className="field">
          <span className="field__label">From the customer</span>
          {order.customerNotes ? <p>{order.customerNotes}</p> : <p className="muted small">None.</p>}
        </div>
        <TextAreaField
          label="Internal notes"
          hint="Only staff see these. Editable at any status."
          rows={4}
          value={notes}
          onChange={(event) => setNotes(event.target.value)}
        />
        {error && <Alert tone="danger">{error}</Alert>}
        <div className="form-actions">
          <Button variant="secondary" disabled={notes.trim() === (order.internalNotes ?? '')} loading={saving} onClick={save}>
            Save notes
          </Button>
        </div>
      </div>
    </Card>
  )
}

// ---- Status: only the transitions the API allows from here ----

function actionLabel(from: OrderStatus, to: OrderStatus): string {
  switch (to) {
    case 'InProgress':
      return from === 'WaitingForCustomer' ? 'Resume repair' : 'Start repair'
    case 'WaitingForCustomer':
      return 'Wait for customer'
    case 'Completed':
      return 'Mark completed'
    case 'Cancelled':
      return 'Cancel order'
    default:
      return statusLabels[to]
  }
}

function StatusCard({ order, reload }: SectionProps) {
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState<OrderStatus | null>(null)
  const [confirmCancel, setConfirmCancel] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const change = async (to: OrderStatus) => {
    setBusy(to)
    setError(null)
    try {
      await adminOrdersApi.changeStatus(order.id, to, reason.trim() || null)
      setReason('')
      setConfirmCancel(false)
      reload()
    } catch (e) {
      // 409: someone else changed the order meanwhile. Show why and the current state.
      setError(errorMessage(e))
      if (e instanceof ApiError && e.status === 409) reload()
    } finally {
      setBusy(null)
    }
  }

  const history = [...order.statusHistory].reverse()

  return (
    <Card title="Status">
      <div className="stack">
        {order.allowedStatusChanges.length > 0 ? (
          <>
            <TextAreaField label="Reason" optional rows={2} placeholder="Saved in the history" value={reason} onChange={(e) => setReason(e.target.value)} />
            <div className="cluster">
              {order.allowedStatusChanges.map((to) => (
                <Button
                  key={to}
                  size="sm"
                  variant={to === 'Cancelled' ? 'secondary' : 'primary'}
                  loading={busy === to}
                  disabled={busy !== null}
                  onClick={() => (to === 'Cancelled' ? setConfirmCancel(true) : change(to))}
                >
                  {actionLabel(order.status, to)}
                </Button>
              ))}
            </div>
          </>
        ) : (
          <p className="muted small">This order is {statusLabels[order.status].toLowerCase()}; its status can't change any more.</p>
        )}
        {error && <Alert tone="danger">{error}</Alert>}

        <ol className="timeline">
          {history.map((entry) => (
            <li key={entry.id} className="timeline__item">
              <div>
                <strong>{statusLabels[entry.to]}</strong> <span className="muted">from {statusLabels[entry.from]}</span>
              </div>
              <div className="timeline__meta">
                {formatDateTime(entry.changedAt)}
                {entry.changedBy && ` · ${entry.changedBy}`}
              </div>
              {entry.reason && <div className="timeline__reason">{entry.reason}</div>}
            </li>
          ))}
          <li className="timeline__item">
            <strong>Booked</strong>
            <div className="timeline__meta">{formatDateTime(order.createdAt)}</div>
          </li>
        </ol>
      </div>

      <ConfirmDialog
        open={confirmCancel}
        title="Cancel this order?"
        message="Cancelling frees the slot on the due date. A cancelled order can't be reopened."
        confirmLabel="Cancel order"
        tone="danger"
        busy={busy === 'Cancelled'}
        onConfirm={() => change('Cancelled')}
        onCancel={() => setConfirmCancel(false)}
      />
    </Card>
  )
}

// ---- Price: discount code (staff) and final price override (Admin) ----

function PriceCard({ order, onChange, reload }: SectionProps) {
  const { hasRole } = useAuth()
  const [code, setCode] = useState('')
  const [codeError, setCodeError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const closed = isClosed(order.status)

  const setDiscount = async (value: string | null) => {
    setBusy(true)
    setCodeError(null)
    try {
      onChange(await adminOrdersApi.patch(order.id, [value ? { op: 'add', path: '/discountCode', value } : { op: 'remove', path: '/discountCode' }]))
      setCode('')
    } catch (e) {
      setCodeError(errorMessage(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <Card title="Price">
      <div className="stack">
        <PriceSummary price={order.price} serviceLabel={order.serviceName} />
        {order.priceOverride && (
          <p className="small muted">
            Set by {order.priceOverride.overriddenBy ?? 'staff'} on {formatDateTime(order.priceOverride.overriddenAt)}: “{order.priceOverride.reason}”
          </p>
        )}

        <div className="field">
          <span className="field__label">Discount code</span>
          {order.price.discountCode ? (
            <span className="chip">
              {order.price.discountCode}
              {!closed && (
                <Button variant="ghost" size="sm" loading={busy} onClick={() => setDiscount(null)} aria-label="Remove discount code">
                  ✕
                </Button>
              )}
            </span>
          ) : closed ? (
            <span className="muted small">None</span>
          ) : (
            <div className="input-group">
              <input className="input" aria-label="Discount code" value={code} onChange={(e) => setCode(e.target.value)} />
              <Button variant="secondary" disabled={!code.trim()} loading={busy} onClick={() => setDiscount(code.trim())}>
                Apply
              </Button>
            </div>
          )}
          {codeError && <span className="field__error">{codeError}</span>}
        </div>

        {hasRole('Admin') && order.status !== 'Cancelled' && <PriceOverrideForm order={order} reload={reload} />}
      </div>
    </Card>
  )
}

function PriceOverrideForm({ order, reload }: { order: AdminOrder; reload: () => void }) {
  const form = useForm({ amount: '', reason: '' })
  const [clearing, setClearing] = useState(false)

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    const done = await form.submit(() => adminOrdersApi.setPriceOverride(order.id, Number(form.values.amount), form.values.reason))
    if (done) {
      form.setValues({ amount: '', reason: '' })
      reload()
    }
  }

  const clear = async () => {
    setClearing(true)
    await form.submit(() => adminOrdersApi.clearPriceOverride(order.id))
    setClearing(false)
    reload()
  }

  return (
    <form className="stack stack--sm" onSubmit={handleSubmit} noValidate>
      <span className="field__label">Final price (admin)</span>
      <div className="grid-2">
        <TextField label="Amount (NOK)" type="number" min={0} step="1" {...form.bind('amount')} />
        <TextField label="Reason" {...form.bind('reason')} />
      </div>
      {form.formError && <Alert tone="danger">{form.formError}</Alert>}
      <div className="form-actions">
        {order.priceOverride && (
          <Button variant="ghost" size="sm" loading={clearing} onClick={clear}>
            Use calculated price
          </Button>
        )}
        <Button type="submit" size="sm" variant="secondary" loading={form.submitting} disabled={!form.values.amount || !form.values.reason}>
          {order.priceOverride ? 'Replace final price' : 'Set final price'}
        </Button>
      </div>
    </form>
  )
}
