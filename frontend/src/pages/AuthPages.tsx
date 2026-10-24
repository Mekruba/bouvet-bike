import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useLocation, useNavigate, useSearchParams } from 'react-router'
import { ApiError, errorMessage } from '../api/client.ts'
import { accountApi } from '../api/endpoints.ts'
import type { CurrentUser } from '../api/types.ts'
import { useAuth } from '../auth/useAuth.ts'
import { Alert } from '../components/Alert.tsx'
import { Button, ButtonLink } from '../components/Button.tsx'
import { Card } from '../components/Card.tsx'
import { Checkbox, TextField } from '../components/Field.tsx'
import { Loading } from '../components/Feedback.tsx'
import { PageHeader } from '../components/PageHeader.tsx'
import { useForm } from '../lib/useForm.ts'

/** Router state passed along with the confirmation link (never put in the URL). */
interface ConfirmationState {
  email?: string
  /** True when the link came straight from the API instead of an email (there's no email delivery). */
  simulatedEmail?: boolean
}

/** Where to go after signing in: back where the user came from, or their natural start page. */
function useAfterSignIn() {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const returnTo = searchParams.get('returnTo')
  return (user: CurrentUser) => navigate(returnTo?.startsWith('/') ? returnTo : user.isStaff ? '/admin' : '/my-orders', { replace: true })
}

/** Follows a confirmation link as if it had been clicked in the email. */
function useOpenConfirmationLink() {
  const navigate = useNavigate()
  return (link: string, email: string) => navigate(link, { state: { email, simulatedEmail: true } satisfies ConfirmationState })
}

export function LoginPage() {
  const { login } = useAuth()
  const afterSignIn = useAfterSignIn()
  const location = useLocation()
  const form = useForm({ email: (location.state as ConfirmationState | null)?.email ?? '', password: '', rememberMe: false })
  const [unconfirmed, setUnconfirmed] = useState(false)

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    setUnconfirmed(false)
    const user = await form.submit(
      () => login(form.values.email, form.values.password, form.values.rememberMe),
      (error) => setUnconfirmed(error instanceof ApiError && error.status === 403),
    )
    if (user) afterSignIn(user)
  }

  return (
    <div className="narrow">
      <PageHeader title="Log in" subtitle="Customers see their orders; staff manage the workshop." />
      <Card footer={<p className="small">New here? <Link to="/register">Create an account</Link>. You can also book without one.</p>}>
        <form className="stack" onSubmit={handleSubmit} noValidate>
          <TextField label="Email" type="email" autoComplete="email" required {...form.bind('email')} />
          <TextField label="Password" type="password" autoComplete="current-password" required {...form.bind('password')} />
          <Checkbox label="Keep me logged in" checked={form.values.rememberMe} onChange={(event) => form.set('rememberMe', event.target.checked)} />
          {unconfirmed ? (
            <Alert tone="warning" title="Confirm your email first">
              <div className="stack stack--sm">
                <span>Your account isn't confirmed yet. Use the link from your confirmation email, or get a new one.</span>
                <ResendConfirmation email={form.values.email} />
              </div>
            </Alert>
          ) : (
            form.formError && <Alert tone="danger">{form.formError}</Alert>
          )}
          <Button type="submit" block loading={form.submitting}>
            Log in
          </Button>
        </form>
      </Card>
    </div>
  )
}

export function RegisterPage() {
  const { register } = useAuth()
  const openConfirmationLink = useOpenConfirmationLink()
  const form = useForm({ name: '', email: '', phone: '', password: '', confirmPassword: '' })
  const [mismatch, setMismatch] = useState(false)

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    const { name, email, phone, password, confirmPassword } = form.values
    // Typed twice to catch typos; the API doesn't need the second one.
    if (password !== confirmPassword) {
      setMismatch(true)
      return
    }
    setMismatch(false)
    const registration = await form.submit(() => register(name, email, phone || null, password))
    if (registration) openConfirmationLink(registration.emailConfirmationLink, registration.user.email)
  }

  const confirmBinding = form.bind('confirmPassword')

  return (
    <div className="narrow">
      <PageHeader
        title="Create an account"
        subtitle="See all your bookings in one place. Bookings you made as a guest with the same email are added once you confirm it."
      />
      <Card footer={<p className="small">Already have an account? <Link to="/login">Log in</Link></p>}>
        <form className="stack" onSubmit={handleSubmit} noValidate>
          <TextField label="Name" autoComplete="name" required {...form.bind('name')} />
          <TextField label="Email" type="email" autoComplete="email" required hint="We send you a link to confirm it." {...form.bind('email')} />
          <TextField label="Phone" type="tel" autoComplete="tel" optional {...form.bind('phone')} />
          <TextField
            label="Password"
            type="password"
            autoComplete="new-password"
            required
            hint="At least 8 characters, with upper and lower case, a digit and a symbol."
            {...form.bind('password')}
          />
          <TextField
            label="Repeat password"
            type="password"
            autoComplete="new-password"
            required
            {...confirmBinding}
            error={mismatch ? 'The passwords don’t match.' : confirmBinding.error}
          />
          {form.formError && <Alert tone="danger">{form.formError}</Alert>}
          <Button type="submit" block loading={form.submitting}>
            Create account
          </Button>
        </form>
      </Card>
    </div>
  )
}

/**
 * The page the confirmation email links to (<c>/confirm-email?userId=…&token=…</c>). It confirms on load.
 * This app sends no email, so right after registering the customer is brought here directly.
 */
export function ConfirmEmailPage() {
  const [params] = useSearchParams()
  const state = useLocation().state as ConfirmationState | null
  const userId = params.get('userId')
  const token = params.get('token')
  const [result, setResult] = useState<{ ok: true } | { ok: false; error: string } | null>(null)

  useEffect(() => {
    if (!userId || !token) return
    let active = true
    accountApi.confirmEmail(userId, token).then(
      () => active && setResult({ ok: true }),
      (error: unknown) => active && setResult({ ok: false, error: errorMessage(error) }),
    )
    return () => {
      active = false
    }
  }, [userId, token])

  const failure = !userId || !token ? 'This confirmation link is incomplete.' : result && !result.ok ? result.error : null

  return (
    <div className="narrow stack">
      <PageHeader title="Confirm your email" />
      {state?.simulatedEmail && (
        <Alert tone="info" title="Demo: no email is sent">
          Normally this page opens from the link in the confirmation email sent to {state.email ?? 'your inbox'}. This demo has no email
          delivery, so you were brought here directly.
        </Alert>
      )}
      <Card>
        {failure ? (
          <div className="stack">
            <Alert tone="danger" title="Couldn't confirm your email">
              {failure}
            </Alert>
            <ResendConfirmation email={state?.email ?? ''} />
          </div>
        ) : result?.ok ? (
          <div className="stack">
            <Alert tone="success" title="Your email is confirmed">
              You can log in now. Any bookings you made as a guest with this email are in your account.
            </Alert>
            <ButtonLink to="/login" state={{ email: state?.email } satisfies ConfirmationState} block>
              Log in
            </ButtonLink>
          </div>
        ) : (
          <Loading label="Confirming your email…" />
        )}
      </Card>
    </div>
  )
}

/** Asks for a new confirmation link and follows it, as clicking it in the new email would. */
function ResendConfirmation({ email: initialEmail }: { email: string }) {
  const openConfirmationLink = useOpenConfirmationLink()
  const [email, setEmail] = useState(initialEmail)
  const [sending, setSending] = useState(false)
  const [message, setMessage] = useState<string | null>(null)

  const resend = async () => {
    setSending(true)
    setMessage(null)
    try {
      const { emailConfirmationLink } = await accountApi.requestEmailConfirmation(email.trim())
      if (emailConfirmationLink) openConfirmationLink(emailConfirmationLink, email.trim())
      else setMessage('There’s no unconfirmed account with that email. Try logging in.')
    } catch (error) {
      setMessage(errorMessage(error))
    } finally {
      setSending(false)
    }
  }

  return (
    <div className="stack stack--sm">
      <div className="input-group">
        <input className="input" type="email" aria-label="Email for a new confirmation link" value={email} onChange={(e) => setEmail(e.target.value)} />
        <Button variant="secondary" loading={sending} disabled={!email.trim()} onClick={resend}>
          Send a new link
        </Button>
      </div>
      {message && <span className="small">{message}</span>}
    </div>
  )
}

export function NotFoundPage() {
  return (
    <div className="narrow">
      <PageHeader title="Page not found" subtitle="The page you were looking for doesn't exist." />
      <Link to="/">Back to booking</Link>
    </div>
  )
}
