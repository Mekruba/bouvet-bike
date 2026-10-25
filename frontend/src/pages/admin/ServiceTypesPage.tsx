import { useState } from 'react'
import type { FormEvent } from 'react'
import { errorMessage } from '../../api/client.ts'
import { catalogApi } from '../../api/endpoints.ts'
import type { ServiceType } from '../../api/types.ts'
import { useApi } from '../../api/useApi.ts'
import { useAuth } from '../../auth/useAuth.ts'
import { Alert } from '../../components/Alert.tsx'
import { Button } from '../../components/Button.tsx'
import { Card } from '../../components/Card.tsx'
import { DataTable } from '../../components/DataTable.tsx'
import type { Column } from '../../components/DataTable.tsx'
import { Dialog } from '../../components/Dialog.tsx'
import { TextAreaField, TextField } from '../../components/Field.tsx'
import { PageHeader } from '../../components/PageHeader.tsx'
import { formatMinutes, formatMoney } from '../../lib/format.ts'
import { diffPatch } from '../../lib/jsonPatch.ts'
import { useForm } from '../../lib/useForm.ts'

interface ServiceForm extends Record<string, unknown> {
  name: string
  description: string
  price: string
  estimatedMinutes: string
}

const emptyForm: ServiceForm = { name: '', description: '', price: '', estimatedMinutes: '' }

const toForm = (s: ServiceType): ServiceForm => ({
  name: s.name,
  description: s.description ?? '',
  price: String(s.price.amount),
  estimatedMinutes: String(s.estimatedMinutes),
})

/** The service catalog. Everyone on staff can see it; admins change it. */
export function ServiceTypesPage() {
  const { hasRole } = useAuth()
  const isAdmin = hasRole('Admin')
  const services = useApi(() => catalogApi.list(true), [])
  const [editing, setEditing] = useState<ServiceType | null>(null)
  const [creating, setCreating] = useState(false)
  const [rowError, setRowError] = useState<string | null>(null)

  const toggleActive = async (service: ServiceType) => {
    setRowError(null)
    try {
      await catalogApi.patch(service.id, [{ op: 'replace', path: '/isActive', value: !service.isActive }])
      services.reload()
    } catch (error) {
      setRowError(errorMessage(error))
    }
  }

  const columns: Column<ServiceType>[] = [
    {
      key: 'name',
      header: 'Service',
      render: (s) => (
        <>
          <div className="table__primary">{s.name}</div>
          {s.description && <div className="table__secondary">{s.description}</div>}
        </>
      ),
    },
    { key: 'duration', header: 'Duration', render: (s) => formatMinutes(s.estimatedMinutes) },
    { key: 'status', header: 'Status', render: (s) => <span className={`badge ${s.isActive ? 'badge--success' : 'badge--muted'}`}>{s.isActive ? 'Bookable' : 'Retired'}</span> },
    { key: 'price', header: 'Price', align: 'right', render: (s) => formatMoney(s.price) },
  ]
  if (isAdmin) {
    columns.push({
      key: 'actions',
      header: <span className="visually-hidden">Actions</span>,
      align: 'right',
      render: (s) => (
        <div className="cluster cluster--end">
          <Button variant="ghost" size="sm" onClick={() => setEditing(s)}>
            Edit
          </Button>
          <Button variant="ghost" size="sm" onClick={() => toggleActive(s)}>
            {s.isActive ? 'Retire' : 'Reactivate'}
          </Button>
        </div>
      ),
    })
  }

  return (
    <div className="stack stack--lg">
      <PageHeader
        title="Services"
        subtitle="What customers can book. Retired services stay on old orders but can't be booked."
        actions={isAdmin && <Button onClick={() => setCreating(true)}>Add service</Button>}
      />
      {rowError && <Alert tone="danger">{rowError}</Alert>}
      {services.error ? (
        <Alert tone="danger">{errorMessage(services.error)}</Alert>
      ) : (
        <Card flush>
          <DataTable columns={columns} rows={services.data} loading={services.loading} rowKey={(s) => s.id} />
        </Card>
      )}

      <ServiceDialog
        key={editing?.id ?? (creating ? 'new' : 'closed')}
        open={creating || editing !== null}
        service={editing}
        onClose={() => {
          setCreating(false)
          setEditing(null)
        }}
        onSaved={() => {
          setCreating(false)
          setEditing(null)
          services.reload()
        }}
      />
    </div>
  )
}

function ServiceDialog({ open, service, onClose, onSaved }: { open: boolean; service: ServiceType | null; onClose: () => void; onSaved: () => void }) {
  const original = service ? toForm(service) : emptyForm
  const form = useForm<ServiceForm>(original)

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    const { name, description, price, estimatedMinutes } = form.values
    const done = await form.submit(() =>
      service
        ? catalogApi.patch(
            service.id,
            diffPatch(
              { ...original, price: Number(original.price), estimatedMinutes: Number(original.estimatedMinutes) },
              { name, description, price: Number(price), estimatedMinutes: Number(estimatedMinutes) },
              ['name', 'description', 'price', 'estimatedMinutes'],
              ['description'],
            ),
          )
        : catalogApi.create({ name, description: description || null, price: Number(price), estimatedMinutes: Number(estimatedMinutes) }),
    )
    if (done) onSaved()
  }

  return (
    <Dialog
      open={open}
      title={service ? `Edit ${service.name}` : 'Add a service'}
      onClose={onClose}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" form="service-form" loading={form.submitting}>
            {service ? 'Save changes' : 'Add service'}
          </Button>
        </>
      }
    >
      <form id="service-form" className="stack" onSubmit={handleSubmit} noValidate>
        <TextField label="Name" required {...form.bind('name')} />
        <TextAreaField label="Description" optional rows={2} {...form.bind('description')} />
        <div className="grid-2">
          <TextField label="Price (NOK)" type="number" min={0} required {...form.bind('price')} />
          <TextField
            label="Estimated minutes"
            type="number"
            min={1}
            required
            hint="A day is 1440 minutes; up to two weeks. Orders for a service taking whole days are due that many days ahead."
            {...form.bind('estimatedMinutes')}
          />
        </div>
        {service && <p className="small muted">Price changes apply to new orders. Existing orders keep the price they were booked at.</p>}
        {form.formError && <Alert tone="danger">{form.formError}</Alert>}
      </form>
    </Dialog>
  )
}
