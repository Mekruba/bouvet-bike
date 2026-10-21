import { useState } from 'react'
import type { FormEvent } from 'react'
import { errorMessage } from '../../api/client.ts'
import { discountsApi } from '../../api/endpoints.ts'
import type { DiscountCode, DiscountType } from '../../api/types.ts'
import { useApi } from '../../api/useApi.ts'
import { Alert } from '../../components/Alert.tsx'
import { Button } from '../../components/Button.tsx'
import { Card } from '../../components/Card.tsx'
import { DataTable } from '../../components/DataTable.tsx'
import type { Column } from '../../components/DataTable.tsx'
import { Dialog } from '../../components/Dialog.tsx'
import { EmptyState } from '../../components/Feedback.tsx'
import { SelectField, TextField } from '../../components/Field.tsx'
import { PageHeader } from '../../components/PageHeader.tsx'
import { formatDate, formatDiscount } from '../../lib/format.ts'
import { diffPatch } from '../../lib/jsonPatch.ts'
import { useForm } from '../../lib/useForm.ts'

interface CodeForm extends Record<string, unknown> {
  code: string
  type: DiscountType
  value: string
  validFrom: string
  validTo: string
  maxUses: string
}

const emptyForm: CodeForm = { code: '', type: 'Percentage', value: '', validFrom: '', validTo: '', maxUses: '' }

const toForm = (c: DiscountCode): CodeForm => ({
  code: c.code,
  type: c.type,
  value: String(c.value),
  validFrom: c.validFrom ?? '',
  validTo: c.validTo ?? '',
  maxUses: c.maxUses === null ? '' : String(c.maxUses),
})

const typeOptions = [
  { value: 'Percentage', label: 'Percentage off' },
  { value: 'FixedAmount', label: 'Fixed amount off (NOK)' },
]

function validity(c: DiscountCode): string {
  if (!c.validFrom && !c.validTo) return 'Always'
  if (!c.validFrom) return `Until ${formatDate(c.validTo!)}`
  if (!c.validTo) return `From ${formatDate(c.validFrom)}`
  return `${formatDate(c.validFrom)} – ${formatDate(c.validTo)}`
}

/** Admin: discount codes customers can use when booking, and staff can apply to orders. */
export function DiscountCodesPage() {
  const codes = useApi(() => discountsApi.list(), [])
  const [editing, setEditing] = useState<DiscountCode | null>(null)
  const [creating, setCreating] = useState(false)
  const [rowError, setRowError] = useState<string | null>(null)

  const toggleActive = async (code: DiscountCode) => {
    setRowError(null)
    try {
      await discountsApi.patch(code.id, [{ op: 'replace', path: '/isActive', value: !code.isActive }])
      codes.reload()
    } catch (error) {
      setRowError(errorMessage(error))
    }
  }

  const columns: Column<DiscountCode>[] = [
    { key: 'code', header: 'Code', render: (c) => <span className="mono table__primary">{c.code}</span> },
    { key: 'discount', header: 'Discount', render: (c) => formatDiscount(c.type, c.value) },
    { key: 'validity', header: 'Valid', render: validity },
    { key: 'uses', header: 'Used', render: (c) => `${c.timesUsed} / ${c.maxUses ?? '∞'}` },
    { key: 'status', header: 'Status', render: (c) => <span className={`badge ${c.isActive ? 'badge--success' : 'badge--muted'}`}>{c.isActive ? 'Active' : 'Inactive'}</span> },
    {
      key: 'actions',
      header: <span className="visually-hidden">Actions</span>,
      align: 'right',
      render: (c) => (
        <div className="cluster cluster--end">
          <Button variant="ghost" size="sm" onClick={() => setEditing(c)}>
            Edit
          </Button>
          <Button variant="ghost" size="sm" onClick={() => toggleActive(c)}>
            {c.isActive ? 'Deactivate' : 'Activate'}
          </Button>
        </div>
      ),
    },
  ]

  return (
    <div className="stack stack--lg">
      <PageHeader
        title="Discount codes"
        subtitle="Codes are matched without regard to case. Orders keep the terms they were booked with."
        actions={<Button onClick={() => setCreating(true)}>New code</Button>}
      />
      {rowError && <Alert tone="danger">{rowError}</Alert>}
      {codes.error ? (
        <Alert tone="danger">{errorMessage(codes.error)}</Alert>
      ) : (
        <Card flush>
          <DataTable
            columns={columns}
            rows={codes.data}
            loading={codes.loading}
            rowKey={(c) => c.id}
            empty={<EmptyState title="No discount codes">Create one to offer customers a discount.</EmptyState>}
          />
        </Card>
      )}

      <CodeDialog
        key={editing?.id ?? (creating ? 'new' : 'closed')}
        open={creating || editing !== null}
        code={editing}
        onClose={() => {
          setCreating(false)
          setEditing(null)
        }}
        onSaved={() => {
          setCreating(false)
          setEditing(null)
          codes.reload()
        }}
      />
    </div>
  )
}

function CodeDialog({ open, code, onClose, onSaved }: { open: boolean; code: DiscountCode | null; onClose: () => void; onSaved: () => void }) {
  const original = code ? toForm(code) : emptyForm
  const form = useForm<CodeForm>(original)

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    const v = form.values
    const terms = {
      type: v.type,
      value: Number(v.value),
      validFrom: v.validFrom || null,
      validTo: v.validTo || null,
      maxUses: v.maxUses ? Number(v.maxUses) : null,
    }
    const done = await form.submit(() =>
      code
        ? discountsApi.patch(
            code.id,
            diffPatch(
              { type: code.type, value: code.value, validFrom: code.validFrom, validTo: code.validTo, maxUses: code.maxUses },
              terms,
              ['type', 'value', 'validFrom', 'validTo', 'maxUses'],
              ['validFrom', 'validTo', 'maxUses'],
            ),
          )
        : discountsApi.create({ code: v.code, ...terms }),
    )
    if (done) onSaved()
  }

  return (
    <Dialog
      open={open}
      title={code ? `Edit ${code.code}` : 'New discount code'}
      onClose={onClose}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" form="code-form" loading={form.submitting}>
            {code ? 'Save changes' : 'Create code'}
          </Button>
        </>
      }
    >
      <form id="code-form" className="stack" onSubmit={handleSubmit} noValidate>
        <TextField
          label="Code"
          required
          disabled={!!code}
          hint={code ? 'A code can’t be renamed; orders keep the code they used.' : 'Letters, digits and dashes.'}
          {...form.bind('code')}
        />
        <div className="grid-2">
          <SelectField label="Type" options={typeOptions} {...form.bind('type')} />
          <TextField label={form.values.type === 'Percentage' ? 'Percent' : 'Amount (NOK)'} type="number" min={0} required {...form.bind('value')} />
          <TextField label="Valid from" type="date" optional {...form.bind('validFrom')} />
          <TextField label="Valid to" type="date" optional {...form.bind('validTo')} />
          <TextField label="Max uses" type="number" min={1} optional hint="Empty for unlimited" {...form.bind('maxUses')} />
        </div>
        {form.formError && <Alert tone="danger">{form.formError}</Alert>}
      </form>
    </Dialog>
  )
}
