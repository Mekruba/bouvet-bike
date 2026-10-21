import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { useNavigate } from 'react-router'
import { ApiError, errorMessage } from '../api/client.ts'
import { catalogApi, ordersApi } from '../api/endpoints.ts'
import type { OrderPrice } from '../api/types.ts'
import { useApi } from '../api/useApi.ts'
import { useAuth } from '../auth/useAuth.ts'
import { Alert } from '../components/Alert.tsx'
import { Button } from '../components/Button.tsx'
import { CapacityStrip } from '../components/CapacityStrip.tsx'
import { Card } from '../components/Card.tsx'
import { AsyncContent, Loading } from '../components/Feedback.tsx'
import { TextAreaField, TextField } from '../components/Field.tsx'
import { PriceSummary } from '../components/PriceSummary.tsx'
import { bikeBrands } from '../lib/bikeBrands.ts'
import { cx } from '../lib/cx.ts'
import { addDays, formatDate, formatMinutes, formatMoney, todayIso } from '../lib/format.ts'
import { rememberContact } from '../lib/orderAccess.ts'
import { useForm } from '../lib/useForm.ts'

const DAYS_SHOWN = 14

/** The customer portal's front page: pick a service and a date, describe the bike, get a confirmation. */
export function BookingPage() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const services = useApi(() => catalogApi.list(), [])

  const [windowStart, setWindowStart] = useState(todayIso)
  const capacity = useApi(() => ordersApi.capacity(windowStart, DAYS_SHOWN), [windowStart])

  const form = useForm({
    serviceTypeId: '',
    dueDate: '',
    bikeBrand: '',
    bikeModel: '',
    name: '',
    email: '',
    phone: '',
    notes: '',
  })
  const { values, set, setValues } = form

  // Logged-in customers don't retype their details.
  useEffect(() => {
    if (user && !user.isStaff) {
      setValues((current) => ({
        ...current,
        name: current.name || user.name,
        email: current.email || user.email,
        phone: current.phone || user.phone || '',
      }))
    }
  }, [user, setValues])

  // Discount code: checked with a price quote when applied, sent with the order once valid.
  const [codeInput, setCodeInput] = useState('')
  const [appliedCode, setAppliedCode] = useState<string | null>(null)
  const [codeError, setCodeError] = useState<string | null>(null)
  const [quote, setQuote] = useState<OrderPrice | null>(null)
  const [nextAvailable, setNextAvailable] = useState<string | null>(null)

  useEffect(() => {
    if (!values.serviceTypeId) return
    let active = true
    ordersApi.quote(values.serviceTypeId, appliedCode).then(
      (price) => active && setQuote(price),
      (error: unknown) => {
        if (!active) return
        setCodeError(errorMessage(error))
        setAppliedCode(null)
      },
    )
    return () => {
      active = false
    }
  }, [values.serviceTypeId, appliedCode])

  const applyCode = async () => {
    const code = codeInput.trim()
    if (!code) return
    if (!values.serviceTypeId) {
      setCodeError('Choose a service first.')
      return
    }
    try {
      const price = await ordersApi.quote(values.serviceTypeId, code)
      setQuote(price)
      setAppliedCode(price.discountCode)
      setCodeError(null)
    } catch (error) {
      setCodeError(errorMessage(error))
    }
  }

  const removeCode = () => {
    setAppliedCode(null)
    setCodeInput('')
  }

  const selectDate = (date: string) => {
    set('dueDate', date)
    setNextAvailable(null)
  }

  const chooseSuggestedDate = (date: string) => {
    selectDate(date)
    setWindowStart(date < addDays(windowStart, DAYS_SHOWN) ? windowStart : date)
    capacity.reload()
  }

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    setNextAvailable(null)
    const order = await form.submit(
      () =>
        ordersApi.place({
          name: values.name,
          email: values.email,
          phone: values.phone,
          bikeBrand: values.bikeBrand,
          bikeModel: values.bikeModel || null,
          serviceTypeId: values.serviceTypeId || null,
          dueDate: values.dueDate || null,
          notes: values.notes || null,
          discountCode: appliedCode,
        }),
      (error) => {
        if (error instanceof ApiError && typeof error.problem?.nextAvailableDate === 'string') {
          setNextAvailable(error.problem.nextAvailableDate)
          capacity.reload()
        }
      },
    )
    if (order) {
      rememberContact(order.reference, values.email)
      navigate(`/orders/${order.reference}?placed=1`)
    }
  }

  const selectedService = services.data?.find((s) => s.id === values.serviceTypeId)
  const canGoBack = windowStart > todayIso()

  return (
    <>
      <section className="hero">
        <span className="hero__eyebrow">Bike maintenance</span>
        <h1>Book a service in two minutes</h1>
        <p>Choose what your bike needs and a day that suits you. We confirm right away and you can follow the repair online.</p>
      </section>

      <form className="with-sidebar" onSubmit={handleSubmit} noValidate>
        <div className="stack stack--lg">
          <Card step={1} title="Choose a service" description="Prices are estimates. The final price is confirmed when the job is done.">
            <AsyncContent {...services}>
              {(list) => (
                <div className="options" role="radiogroup" aria-label="Service">
                  {list.map((service) => (
                    <button
                      key={service.id}
                      type="button"
                      role="radio"
                      aria-checked={values.serviceTypeId === service.id}
                      className={cx('option', values.serviceTypeId === service.id && 'is-selected')}
                      onClick={() => set('serviceTypeId', service.id)}
                    >
                      <span className="option__title">{service.name}</span>
                      {service.description && <span className="option__text">{service.description}</span>}
                      <span className="option__meta">
                        <span className="option__price">{formatMoney(service.price)}</span>
                        <span className="muted">≈ {formatMinutes(service.estimatedMinutes)}</span>
                      </span>
                    </button>
                  ))}
                </div>
              )}
            </AsyncContent>
            {form.fieldErrors.serviceTypeId && <p className="field__error">Choose a service.</p>}
          </Card>

          <Card
            step={2}
            title="Pick a due date"
            description="When you'd like the bike ready. Each day has room for a limited number of bikes."
            actions={
              <>
                <Button variant="ghost" size="sm" disabled={!canGoBack} onClick={() => setWindowStart(addDays(windowStart, -DAYS_SHOWN))}>
                  ← Earlier
                </Button>
                <Button variant="ghost" size="sm" onClick={() => setWindowStart(addDays(windowStart, DAYS_SHOWN))}>
                  Later →
                </Button>
              </>
            }
          >
            <div className="stack">
              {capacity.data ? (
                <CapacityStrip days={capacity.data} selected={values.dueDate} onSelect={selectDate} />
              ) : (
                <AsyncContent {...capacity}>{() => null}</AsyncContent>
              )}
              {form.fieldErrors.dueDate && <p className="field__error">Choose a due date.</p>}
              {nextAvailable && (
                <Alert
                  tone="warning"
                  title="That day just filled up"
                  action={
                    <Button size="sm" onClick={() => chooseSuggestedDate(nextAvailable)}>
                      Choose {formatDate(nextAvailable)}
                    </Button>
                  }
                >
                  The next day with room is {formatDate(nextAvailable)}.
                </Alert>
              )}
            </div>
          </Card>

          <Card step={3} title="Your bike">
            <div className="grid-2">
              <TextField label="Brand" list="bike-brands" autoComplete="off" required {...form.bind('bikeBrand')} />
              <TextField label="Model" optional placeholder="e.g. Domane SL5" {...form.bind('bikeModel')} />
              <TextAreaField
                className="span-2"
                label="What should we know?"
                optional
                hint="Symptoms, noises, anything special. You can change this until we start the repair."
                rows={3}
                {...form.bind('notes')}
              />
            </div>
            <datalist id="bike-brands">
              {bikeBrands.map((brand) => (
                <option key={brand} value={brand} />
              ))}
            </datalist>
          </Card>

          <Card step={4} title="Your details" description="We only use these to contact you about this repair.">
            <div className="grid-2">
              <TextField className="span-2" label="Name" autoComplete="name" required {...form.bind('name')} />
              <TextField label="Email" type="email" autoComplete="email" required {...form.bind('email')} />
              <TextField label="Phone" type="tel" autoComplete="tel" required hint="8 digits, or with country code" {...form.bind('phone')} />
            </div>
          </Card>
        </div>

        <aside className="with-sidebar__aside">
          <Card title="Summary">
            <div className="stack">
              <dl className="details">
                <dt>Service</dt>
                <dd>{selectedService?.name ?? <span className="muted">Not chosen</span>}</dd>
                <dt>Due date</dt>
                <dd>{values.dueDate ? formatDate(values.dueDate) : <span className="muted">Not chosen</span>}</dd>
              </dl>

              {values.serviceTypeId ? quote ? <PriceSummary price={quote} serviceLabel={selectedService?.name} totalLabel="Estimated total" /> : <Loading label="Calculating price…" /> : null}

              <div className="field">
                <label className="field__label" htmlFor="discount-code">
                  Discount code
                </label>
                {appliedCode ? (
                  <span className="chip">
                    {appliedCode}
                    <Button variant="ghost" size="sm" onClick={removeCode} aria-label="Remove discount code">
                      ✕
                    </Button>
                  </span>
                ) : (
                  <div className="input-group">
                    <input
                      id="discount-code"
                      className="input"
                      value={codeInput}
                      onChange={(event) => setCodeInput(event.target.value)}
                      onKeyDown={(event) => {
                        if (event.key === 'Enter') {
                          event.preventDefault()
                          void applyCode()
                        }
                      }}
                    />
                    <Button variant="secondary" onClick={() => void applyCode()}>
                      Apply
                    </Button>
                  </div>
                )}
                {codeError && <span className="field__error">{codeError}</span>}
              </div>

              {form.formError && <Alert tone="danger">{form.formError}</Alert>}

              <Button type="submit" size="lg" block loading={form.submitting}>
                Confirm booking
              </Button>
              <p className="small muted">No online payment. You get a confirmation with your order reference right away.</p>
            </div>
          </Card>
        </aside>
      </form>
    </>
  )
}
