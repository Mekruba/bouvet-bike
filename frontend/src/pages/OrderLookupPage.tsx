import type { FormEvent } from 'react'
import { useNavigate, useSearchParams } from 'react-router'
import { ApiError } from '../api/client.ts'
import { ordersApi } from '../api/endpoints.ts'
import { Alert } from '../components/Alert.tsx'
import { Button } from '../components/Button.tsx'
import { Card } from '../components/Card.tsx'
import { TextField } from '../components/Field.tsx'
import { PageHeader } from '../components/PageHeader.tsx'
import { rememberContact } from '../lib/orderAccess.ts'
import { useForm } from '../lib/useForm.ts'

/** Guests find their order with the reference plus the email or phone number on it. */
export function OrderLookupPage() {
  const [searchParams] = useSearchParams()
  const navigate = useNavigate()
  const form = useForm({ reference: searchParams.get('reference') ?? '', contact: '' })

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    const order = await form.submit(async () => {
      try {
        return await ordersApi.get(form.values.reference.trim(), form.values.contact.trim())
      } catch (error) {
        // The API answers 404 for both "no such order" and "wrong contact", on purpose.
        throw error instanceof ApiError && error.status === 404
          ? new Error('We couldn’t find an order with that reference and email or phone number.')
          : error
      }
    })
    if (order) {
      rememberContact(order.reference, form.values.contact.trim())
      navigate(`/orders/${order.reference}`)
    }
  }

  return (
    <div className="narrow">
      <PageHeader title="Find my order" subtitle="See the status of your repair and update your notes." />
      <Card>
        <form className="stack" onSubmit={handleSubmit} noValidate>
          {searchParams.has('missing') && !form.formError && (
            <Alert tone="info">Enter the email or phone number you booked with to see this order.</Alert>
          )}
          <TextField
            label="Order reference"
            placeholder="BB-XXXXXXXX"
            hint="From your booking confirmation"
            autoCapitalize="characters"
            required
            {...form.bind('reference')}
          />
          <TextField label="Email or phone number" autoComplete="email" required {...form.bind('contact')} />
          {form.formError && <Alert tone="danger">{form.formError}</Alert>}
          <Button type="submit" block loading={form.submitting}>
            Find order
          </Button>
        </form>
      </Card>
    </div>
  )
}
