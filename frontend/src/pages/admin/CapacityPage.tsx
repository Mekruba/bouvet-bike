import { useState } from 'react'
import type { FormEvent } from 'react'
import { capacityApi, ordersApi } from '../../api/endpoints.ts'
import type { CapacityOverride, CapacitySchedule, DayCapacity, Weekday } from '../../api/types.ts'
import { useApi } from '../../api/useApi.ts'
import { useAuth } from '../../auth/useAuth.ts'
import { Alert } from '../../components/Alert.tsx'
import { Button } from '../../components/Button.tsx'
import { CapacityStrip } from '../../components/CapacityStrip.tsx'
import { Card } from '../../components/Card.tsx'
import { DataTable } from '../../components/DataTable.tsx'
import type { Column } from '../../components/DataTable.tsx'
import { Dialog } from '../../components/Dialog.tsx'
import { AsyncContent, EmptyState } from '../../components/Feedback.tsx'
import { TextField } from '../../components/Field.tsx'
import { PageHeader } from '../../components/PageHeader.tsx'
import { addDays, formatDate, todayIso } from '../../lib/format.ts'
import { diffPatch } from '../../lib/jsonPatch.ts'
import { useForm } from '../../lib/useForm.ts'

const CALENDAR_DAYS = 28

const weekdays: Array<{ key: Weekday; label: string }> = [
  { key: 'monday', label: 'Monday' },
  { key: 'tuesday', label: 'Tuesday' },
  { key: 'wednesday', label: 'Wednesday' },
  { key: 'thursday', label: 'Thursday' },
  { key: 'friday', label: 'Friday' },
  { key: 'saturday', label: 'Saturday' },
  { key: 'sunday', label: 'Sunday' },
]

/** "6/5 (+1)", "0/5 (closed)", "3/5 (−2)": a date's capacity against its weekday default. */
function deviation(capacity: number, defaultCapacity: number): string {
  if (capacity === 0) return `0/${defaultCapacity} (closed)`
  const difference = capacity - defaultCapacity
  return `${capacity}/${defaultCapacity} (${difference > 0 ? '+' : '−'}${Math.abs(difference)})`
}

/** How many bikes the workshop takes per day: weekday defaults, and dates that deviate from them. */
export function CapacityPage() {
  const { hasRole } = useAuth()
  const isAdmin = hasRole('Admin')
  const [windowStart, setWindowStart] = useState(todayIso)
  const schedule = useApi(() => capacityApi.schedule(), [])
  const calendar = useApi(() => ordersApi.capacity(windowStart, CALENDAR_DAYS), [windowStart])
  const overrides = useApi(() => capacityApi.overrides(todayIso(), addDays(todayIso(), 180)), [])
  const [editing, setEditing] = useState<DayCapacity | null>(null)

  const refresh = () => {
    calendar.reload()
    overrides.reload()
  }

  const columns: Column<CapacityOverride>[] = [
    { key: 'date', header: 'Date', render: (o) => <span className="table__primary">{formatDate(o.date)}</span> },
    {
      key: 'capacity',
      header: 'Capacity',
      render: (o) => <span className={`badge ${o.maxOrders === 0 ? 'badge--muted' : 'badge--primary'}`}>{deviation(o.maxOrders, o.defaultCapacity)}</span>,
    },
    { key: 'reason', header: 'Reason', render: (o) => o.reason ?? <span className="muted">–</span> },
  ]
  if (isAdmin) {
    columns.push({
      key: 'actions',
      header: <span className="visually-hidden">Actions</span>,
      align: 'right',
      render: (o) => (
        <Button
          variant="ghost"
          size="sm"
          onClick={() => setEditing(calendar.data?.find((d) => d.date === o.date) ?? { date: o.date, booked: 0, capacity: o.maxOrders, defaultCapacity: o.defaultCapacity, remaining: 0, isFull: false, isClosed: o.maxOrders === 0, isAdjusted: true })}
        >
          Edit
        </Button>
      ),
    })
  }

  return (
    <div className="stack stack--lg">
      <PageHeader
        title="Capacity"
        subtitle="How many bikes the workshop takes per due date. Customers can only book days with room left."
      />

      <div className="with-sidebar">
        <Card
          title="Calendar"
          description={isAdmin ? 'Click a day to give it more or less capacity, or close it.' : 'Booked and capacity per day.'}
          actions={
            <>
              <Button variant="ghost" size="sm" disabled={windowStart <= todayIso()} onClick={() => setWindowStart(addDays(windowStart, -CALENDAR_DAYS))}>
                ← Earlier
              </Button>
              <Button variant="ghost" size="sm" onClick={() => setWindowStart(addDays(windowStart, CALENDAR_DAYS))}>
                Later →
              </Button>
            </>
          }
        >
          <AsyncContent {...calendar}>
            {(days) => <CapacityStrip days={days} detailed allowFull onSelect={isAdmin ? (date) => setEditing(days.find((d) => d.date === date) ?? null) : undefined} />}
          </AsyncContent>
        </Card>

        <AsyncContent {...schedule}>
          {(data) => <ScheduleCard key={JSON.stringify(data)} schedule={data} editable={isAdmin} onSaved={(saved) => { schedule.setData(saved); refresh() }} />}
        </AsyncContent>
      </div>

      <Card title="Adjusted dates" description="Dates whose capacity differs from their weekday default, next six months." flush>
        <DataTable
          columns={columns}
          rows={overrides.data}
          loading={overrides.loading}
          rowKey={(o) => o.date}
          empty={<EmptyState title="No adjusted dates">Every day uses its weekday default.</EmptyState>}
        />
      </Card>

      <DayDialog
        key={editing?.date ?? 'closed'}
        day={editing}
        onClose={() => setEditing(null)}
        onSaved={() => {
          setEditing(null)
          refresh()
        }}
      />
    </div>
  )
}

function ScheduleCard({ schedule, editable, onSaved }: { schedule: CapacitySchedule; editable: boolean; onSaved: (schedule: CapacitySchedule) => void }) {
  const initial = Object.fromEntries(weekdays.map(({ key }) => [key, String(schedule[key])])) as Record<Weekday, string>
  const form = useForm(initial)
  const asNumbers = (values: Record<Weekday, string>) =>
    Object.fromEntries(weekdays.map(({ key }) => [key, Number(values[key])])) as CapacitySchedule
  const patch = diffPatch(schedule, asNumbers(form.values), weekdays.map((w) => w.key))

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    const saved = await form.submit(() => capacityApi.patchSchedule(patch))
    if (saved) onSaved(saved)
  }

  return (
    <Card title="Weekly defaults" description="Bikes per day for each weekday. 0 means closed.">
      <form className="stack" onSubmit={handleSubmit} noValidate>
        <fieldset className="fieldset grid-2" disabled={!editable}>
          {weekdays.map(({ key, label }) => (
            <TextField key={key} label={label} type="number" min={0} {...form.bind(key)} />
          ))}
        </fieldset>
        {form.formError && <Alert tone="danger">{form.formError}</Alert>}
        {editable && (
          <div className="form-actions">
            <Button variant="ghost" disabled={patch.length === 0} onClick={() => form.setValues(initial)}>
              Reset
            </Button>
            <Button type="submit" disabled={patch.length === 0} loading={form.submitting}>
              Save defaults
            </Button>
          </div>
        )}
      </form>
    </Card>
  )
}

function DayDialog({ day, onClose, onSaved }: { day: DayCapacity | null; onClose: () => void; onSaved: () => void }) {
  const form = useForm({ maxOrders: day ? String(day.capacity) : '', reason: '' })
  const [resetting, setResetting] = useState(false)

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    if (!day) return
    const saved = await form.submit(() => capacityApi.setOverride(day.date, Number(form.values.maxOrders), form.values.reason || null))
    if (saved) onSaved()
  }

  const reset = async () => {
    if (!day) return
    setResetting(true)
    const done = await form.submit(() => capacityApi.removeOverride(day.date).then(() => true))
    setResetting(false)
    if (done) onSaved()
  }

  return (
    <Dialog
      open={day !== null}
      title={day ? formatDate(day.date) : ''}
      onClose={onClose}
      footer={
        <>
          {day?.isAdjusted && (
            <Button variant="ghost" loading={resetting} onClick={reset}>
              Use default ({day.defaultCapacity})
            </Button>
          )}
          <Button variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" form="day-form" loading={form.submitting}>
            Save
          </Button>
        </>
      }
    >
      {day && (
        <form id="day-form" className="stack" onSubmit={handleSubmit} noValidate>
          <p className="small muted">
            Default for this weekday: {day.defaultCapacity}. Booked: {day.booked}.
            {day.isAdjusted && ` Currently ${deviation(day.capacity, day.defaultCapacity)}.`}
          </p>
          <TextField label="Capacity for this date" type="number" min={0} hint="0 closes the day for new bookings." {...form.bind('maxOrders')} />
          <TextField label="Reason" optional placeholder="e.g. Public holiday, extra mechanic" {...form.bind('reason')} />
          {Number(form.values.maxOrders) < day.booked && form.values.maxOrders !== '' && (
            <Alert tone="warning">{day.booked} bikes are already booked. They stay; the day just won't take new bookings.</Alert>
          )}
          {form.formError && <Alert tone="danger">{form.formError}</Alert>}
        </form>
      )}
    </Dialog>
  )
}
