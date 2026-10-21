import { useState } from 'react'
import type { FormEvent } from 'react'
import { errorMessage } from '../../api/client.ts'
import { accountApi } from '../../api/endpoints.ts'
import type { Role, StaffUser } from '../../api/types.ts'
import { useApi } from '../../api/useApi.ts'
import { Alert } from '../../components/Alert.tsx'
import { Button } from '../../components/Button.tsx'
import { Card } from '../../components/Card.tsx'
import { DataTable } from '../../components/DataTable.tsx'
import type { Column } from '../../components/DataTable.tsx'
import { Dialog } from '../../components/Dialog.tsx'
import { SelectField, TextField } from '../../components/Field.tsx'
import { PageHeader } from '../../components/PageHeader.tsx'
import { initials } from '../../lib/format.ts'
import { useForm } from '../../lib/useForm.ts'

const columns: Column<StaffUser>[] = [
  {
    key: 'name',
    header: 'Name',
    render: (s) => (
      <span className="user-chip">
        <span className="avatar" aria-hidden>
          {initials(s.name)}
        </span>
        {s.name}
      </span>
    ),
  },
  { key: 'email', header: 'Email', render: (s) => s.email },
  {
    key: 'roles',
    header: 'Role',
    render: (s) => (
      <span className="cluster">
        {s.roles.map((role) => (
          <span key={role} className={`badge ${role === 'Admin' ? 'badge--primary' : 'badge--muted'}`}>
            {role}
          </span>
        ))}
      </span>
    ),
  },
]

const roleOptions = [
  { value: 'Mechanic', label: 'Mechanic: orders and workshop' },
  { value: 'Admin', label: 'Admin: also prices, discounts and staff' },
]

/** Admin: staff accounts. Customers register themselves and aren't listed here. */
export function StaffPage() {
  const staff = useApi(() => accountApi.staff(), [])
  const [creating, setCreating] = useState(false)

  return (
    <div className="stack stack--lg">
      <PageHeader
        title="Staff"
        subtitle="Mechanics manage orders. Admins also manage prices, discount codes and staff."
        actions={<Button onClick={() => setCreating(true)}>Add staff member</Button>}
      />
      {staff.error ? (
        <Alert tone="danger">{errorMessage(staff.error)}</Alert>
      ) : (
        <Card flush>
          <DataTable columns={columns} rows={staff.data} loading={staff.loading} rowKey={(s) => s.id} />
        </Card>
      )}
      <StaffDialog
        key={creating ? 'open' : 'closed'}
        open={creating}
        onClose={() => setCreating(false)}
        onSaved={() => {
          setCreating(false)
          staff.reload()
        }}
      />
    </div>
  )
}

function StaffDialog({ open, onClose, onSaved }: { open: boolean; onClose: () => void; onSaved: () => void }) {
  const form = useForm({ name: '', email: '', password: '', role: 'Mechanic' as Role })

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    const { name, email, password, role } = form.values
    if (await form.submit(() => accountApi.createStaff(name, email, password, role))) onSaved()
  }

  return (
    <Dialog
      open={open}
      title="Add a staff member"
      onClose={onClose}
      footer={
        <>
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" form="staff-form" loading={form.submitting}>
            Add staff member
          </Button>
        </>
      }
    >
      <form id="staff-form" className="stack" onSubmit={handleSubmit} noValidate>
        <TextField label="Name" required {...form.bind('name')} />
        <TextField label="Email" type="email" required {...form.bind('email')} />
        <TextField
          label="Temporary password"
          type="password"
          autoComplete="new-password"
          required
          hint="At least 8 characters, with upper and lower case, a digit and a symbol. Share it with them in person."
          {...form.bind('password')}
        />
        <SelectField label="Role" options={roleOptions} {...form.bind('role')} />
        {form.formError && <Alert tone="danger">{form.formError}</Alert>}
      </form>
    </Dialog>
  )
}
