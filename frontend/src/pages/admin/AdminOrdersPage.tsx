import { useEffect, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router'
import { errorMessage } from '../../api/client.ts'
import { adminOrdersApi, catalogApi, ordersApi } from '../../api/endpoints.ts'
import type { AdminOrderQuery, AdminOrderSummary, OrderSort, OrderStatus } from '../../api/types.ts'
import { useApi } from '../../api/useApi.ts'
import { Alert } from '../../components/Alert.tsx'
import { Button } from '../../components/Button.tsx'
import { CapacityStrip } from '../../components/CapacityStrip.tsx'
import { Card } from '../../components/Card.tsx'
import { DataTable } from '../../components/DataTable.tsx'
import type { Column, SortState } from '../../components/DataTable.tsx'
import { EmptyState } from '../../components/Feedback.tsx'
import { SelectField, TextField } from '../../components/Field.tsx'
import { PageHeader } from '../../components/PageHeader.tsx'
import { Pagination } from '../../components/Pagination.tsx'
import { StatusBadge } from '../../components/StatusBadge.tsx'
import { allStatuses, formatDate, formatDateTime, formatMoney, statusLabels, todayIso } from '../../lib/format.ts'

const PAGE_SIZE = 20

const columns: Column<AdminOrderSummary>[] = [
  {
    key: 'reference',
    header: 'Order',
    sortKey: 'CreatedAt',
    render: (o) => (
      <>
        <div className="mono table__primary">{o.reference}</div>
        <div className="table__secondary">{formatDateTime(o.createdAt)}</div>
      </>
    ),
  },
  {
    key: 'customer',
    header: 'Customer',
    sortKey: 'CustomerName',
    render: (o) => (
      <>
        <div className="table__primary">
          {o.contact.name} {!o.isGuest && <span className="badge badge--primary">Account</span>}
        </div>
        <div className="table__secondary">
          {o.contact.email} · {o.contact.phone}
        </div>
      </>
    ),
  },
  {
    key: 'job',
    header: 'Service',
    render: (o) => (
      <>
        <div className="table__primary">{o.serviceName}</div>
        <div className="table__secondary">{o.bikeBrand}</div>
      </>
    ),
  },
  { key: 'due', header: 'Due', sortKey: 'DueDate', render: (o) => formatDate(o.dueDate) },
  { key: 'status', header: 'Status', sortKey: 'Status', render: (o) => <StatusBadge status={o.status} /> },
  { key: 'total', header: 'Total', align: 'right', render: (o) => formatMoney(o.total) },
]

/** Counters at the top: each one is also a quick filter. */
const counters: Array<{ label: string; filter: Partial<Record<'status' | 'dueFrom' | 'dueTo', string>> }> = [
  { label: 'New, not started', filter: { status: 'New' } },
  { label: 'In progress', filter: { status: 'InProgress' } },
  { label: 'Waiting for customer', filter: { status: 'WaitingForCustomer' } },
  { label: 'Due today', filter: { dueFrom: todayIso(), dueTo: todayIso() } },
]

/** Staff overview: today's numbers, capacity for the next two weeks, and the searchable order list. */
export function AdminOrdersPage() {
  const navigate = useNavigate()
  const [params, setParams] = useSearchParams()

  const query: AdminOrderQuery = {
    search: params.get('search') ?? undefined,
    status: (params.get('status') as OrderStatus | null) ?? undefined,
    serviceTypeId: params.get('serviceTypeId') ?? undefined,
    dueFrom: params.get('dueFrom') ?? undefined,
    dueTo: params.get('dueTo') ?? undefined,
    sort: (params.get('sort') as OrderSort | null) ?? 'DueDate',
    descending: params.get('descending') === 'true',
    page: Number(params.get('page') ?? 1),
    pageSize: PAGE_SIZE,
  }

  /** Filters live in the URL, so the back button and shared links keep them. */
  const update = (changes: Record<string, string | undefined>, resetPage = true) => {
    const next = new URLSearchParams(params)
    for (const [key, value] of Object.entries(changes)) {
      if (value) next.set(key, value)
      else next.delete(key)
    }
    if (resetPage) next.delete('page')
    setParams(next, { replace: true })
  }

  const orders = useApi(() => adminOrdersApi.search(query), [params.toString()])
  const services = useApi(() => catalogApi.list(true), [])
  const capacity = useApi(() => ordersApi.capacity(todayIso(), 14), [])
  const counts = useApi(
    () => Promise.all(counters.map((c) => adminOrdersApi.search({ ...c.filter, pageSize: 1 } as AdminOrderQuery).then((r) => r.totalCount))),
    [],
  )

  // Search as you type, without a request per keystroke.
  const [searchText, setSearchText] = useState(query.search ?? '')
  useEffect(() => {
    const timer = setTimeout(() => {
      if (searchText !== (params.get('search') ?? '')) update({ search: searchText.trim() || undefined })
    }, 300)
    return () => clearTimeout(timer)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchText])

  const hasFilters = ['search', 'status', 'serviceTypeId', 'dueFrom', 'dueTo'].some((key) => params.has(key))
  const selectedDay = query.dueFrom && query.dueFrom === query.dueTo ? query.dueFrom : null

  return (
    <div className="stack stack--lg">
      <PageHeader title="Orders" subtitle="Everything booked in the workshop." />

      <div className="grid-2">
        <Card title="Today">
          <div className="grid-2">
            {counters.map((counter, index) => (
              <button
                key={counter.label}
                type="button"
                className="option"
                onClick={() => setParams(new URLSearchParams(counter.filter as Record<string, string>), { replace: true })}
              >
                <span className="stat">
                  <span className="stat__value">{counts.data?.[index] ?? '–'}</span>
                  <span className="stat__label">{counter.label}</span>
                </span>
              </button>
            ))}
          </div>
        </Card>
        <Card title="Capacity, next 14 days" description="Click a day to see its orders.">
          {capacity.data && (
            <CapacityStrip
              days={capacity.data}
              selected={selectedDay}
              allowFull
              onSelect={(day) =>
                update(selectedDay === day ? { dueFrom: undefined, dueTo: undefined } : { dueFrom: day, dueTo: day })
              }
            />
          )}
        </Card>
      </div>

      <Card flush>
        <div className="toolbar">
          <TextField
            label="Search"
            type="search"
            placeholder="Reference, name, email, phone or bike"
            value={searchText}
            onChange={(event) => setSearchText(event.target.value)}
          />
          <SelectField
            label="Status"
            placeholder="All"
            value={query.status ?? ''}
            options={allStatuses.map((status) => ({ value: status, label: statusLabels[status] }))}
            onChange={(event) => update({ status: event.target.value || undefined })}
          />
          <SelectField
            label="Service"
            placeholder="All"
            value={query.serviceTypeId ?? ''}
            options={(services.data ?? []).map((s) => ({ value: s.id, label: s.name }))}
            onChange={(event) => update({ serviceTypeId: event.target.value || undefined })}
          />
          <TextField label="Due from" type="date" value={query.dueFrom ?? ''} onChange={(event) => update({ dueFrom: event.target.value || undefined })} />
          <TextField label="Due to" type="date" value={query.dueTo ?? ''} onChange={(event) => update({ dueTo: event.target.value || undefined })} />
          <Button
            variant="ghost"
            disabled={!hasFilters}
            onClick={() => {
              setSearchText('')
              setParams(new URLSearchParams(), { replace: true })
            }}
          >
            Clear
          </Button>
        </div>

        {orders.error ? (
          <div className="card__body">
            <Alert tone="danger">{errorMessage(orders.error)}</Alert>
          </div>
        ) : (
          <>
            <DataTable
              columns={columns}
              rows={orders.data?.items}
              loading={orders.loading}
              rowKey={(o) => o.id}
              onRowClick={(o) => navigate(`/admin/orders/${o.id}`)}
              sort={{ key: query.sort ?? 'DueDate', descending: query.descending ?? false }}
              onSortChange={(sort: SortState) => update({ sort: sort.key, descending: sort.descending ? 'true' : undefined })}
              empty={<EmptyState title="No orders match">Try another search or clear the filters.</EmptyState>}
            />
            {orders.data && orders.data.totalCount > 0 && (
              <Pagination
                page={orders.data.page}
                pageSize={orders.data.pageSize}
                totalCount={orders.data.totalCount}
                onPageChange={(page) => update({ page: String(page) }, false)}
              />
            )}
          </>
        )}
      </Card>
    </div>
  )
}
