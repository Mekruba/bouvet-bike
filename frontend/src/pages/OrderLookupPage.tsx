import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { useNavigate, useSearchParams } from 'react-router'
import { ApiError } from '../api/client.ts'
import { ordersApi } from '../api/endpoints.ts'
import type { CustomerOrderSummary } from '../api/types.ts'
import { Alert } from '../components/Alert.tsx'
import { Button } from '../components/Button.tsx'
import { Card } from '../components/Card.tsx'
import { EmptyState } from '../components/Feedback.tsx'
import { TextField } from '../components/Field.tsx'
import { OrderSummaryTable } from '../components/OrderSummaryTable.tsx'
import { PageHeader } from '../components/PageHeader.tsx'
import { recallSearch, rememberSearch } from '../lib/orderAccess.ts'
import { useForm } from '../lib/useForm.ts'

interface SearchResults {
  contact: string
  orders: CustomerOrderSummary[]
}

/**
 * Guests find orders in one of two ways: the booking reference opens that order directly; an email or phone
 * number lists every order booked with it.
 */
export function OrderLookupPage() {
  const [results, setResults] = useState<SearchResults | null>(null)

  return (
    <div className="stack stack--lg">
      <PageHeader title="Find my order" subtitle="See the status of your repair, your booking history, and update your notes." />
      <div className="grid-2">
        <ByReference />
        <ByContact onResults={setResults} />
      </div>
      {results && (
        <Card
          flush
          title="Your bookings"
          description={
            results.orders.length === 0
              ? `No bookings found for ${results.contact}.`
              : `${results.orders.length} booking${results.orders.length === 1 ? '' : 's'} for ${results.contact}, newest first. Open one to see details or change notes.`
          }
        >
          {results.orders.length > 0 && <OrderSummaryTable orders={results.orders} empty={<EmptyState title="No bookings" />} />}
        </Card>
      )}
    </div>
  )
}

function ByReference() {
  const [searchParams] = useSearchParams()
  const navigate = useNavigate()
  const form = useForm({ reference: searchParams.get('reference') ?? '' })

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    const order = await form.submit(async () => {
      const reference = form.values.reference.trim()
      if (!reference) throw new Error('Enter the reference from your booking confirmation.')
      try {
        return await ordersApi.get(reference)
      } catch (error) {
        throw error instanceof ApiError && error.status === 404 ? new Error(`No order with the reference ${reference}.`) : error
      }
    })
    if (order) navigate(`/orders/${order.reference}`)
  }

  return (
    <Card title="With your booking reference" description="Opens that order.">
      <form className="stack" onSubmit={handleSubmit} noValidate>
        <TextField label="Booking reference" placeholder="BB-XXXXXXXX" autoCapitalize="characters" {...form.bind('reference')} />
        {form.formError && <Alert tone="danger">{form.formError}</Alert>}
        <Button type="submit" block loading={form.submitting}>
          Open order
        </Button>
      </form>
    </Card>
  )
}

function ByContact({ onResults }: { onResults: (results: SearchResults) => void }) {
  const form = useForm({ contact: recallSearch() })
  const { submit } = form

  const search = async (contact: string) => {
    const orders = await submit(async () => {
      if (!contact) throw new Error('Enter the email or phone number you booked with.')
      return ordersApi.findByContact(contact)
    })
    if (orders) {
      rememberSearch(contact)
      onResults({ contact, orders })
    }
  }

  // Coming back from an order shows the last search again (this tab only).
  useEffect(() => {
    const last = recallSearch()
    if (last) void search(last)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault()
    void search(form.values.contact.trim())
  }

  return (
    <Card title="With your email or phone" description="Lists every booking made with it.">
      <form className="stack" onSubmit={handleSubmit} noValidate>
        <TextField label="Email or phone number" autoComplete="email" {...form.bind('contact')} />
        {form.formError && <Alert tone="danger">{form.formError}</Alert>}
        <Button type="submit" variant="secondary" block loading={form.submitting}>
          Find my bookings
        </Button>
      </form>
    </Card>
  )
}
