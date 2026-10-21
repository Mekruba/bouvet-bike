import type { FormEvent } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router'
import type { CurrentUser } from '../api/types.ts'
import { useAuth } from '../auth/useAuth.ts'
import { Alert } from '../components/Alert.tsx'
import { Button } from '../components/Button.tsx'
import { Card } from '../components/Card.tsx'
import { Checkbox, TextField } from '../components/Field.tsx'
import { PageHeader } from '../components/PageHeader.tsx'
import { useForm } from '../lib/useForm.ts'

/** Where to go after signing in: back where the user came from, or their natural start page. */
function useAfterSignIn() {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const returnTo = searchParams.get('returnTo')
  return (user: CurrentUser) => navigate(returnTo?.startsWith('/') ? returnTo : user.isStaff ? '/admin' : '/my-orders', { replace: true })
}

export function LoginPage() {
  const { login } = useAuth()
  const afterSignIn = useAfterSignIn()
  const form = useForm({ email: '', password: '', rememberMe: false })

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    const user = await form.submit(() => login(form.values.email, form.values.password, form.values.rememberMe))
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
          {form.formError && <Alert tone="danger">{form.formError}</Alert>}
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
  const afterSignIn = useAfterSignIn()
  const form = useForm({ name: '', email: '', phone: '', password: '' })

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    const { name, email, phone, password } = form.values
    const user = await form.submit(() => register(name, email, phone || null, password))
    if (user) afterSignIn(user)
  }

  return (
    <div className="narrow">
      <PageHeader title="Create an account" subtitle="See all your bookings in one place. Booking without an account works too." />
      <Card footer={<p className="small">Already have an account? <Link to="/login">Log in</Link></p>}>
        <form className="stack" onSubmit={handleSubmit} noValidate>
          <TextField label="Name" autoComplete="name" required {...form.bind('name')} />
          <TextField label="Email" type="email" autoComplete="email" required {...form.bind('email')} />
          <TextField label="Phone" type="tel" autoComplete="tel" optional {...form.bind('phone')} />
          <TextField
            label="Password"
            type="password"
            autoComplete="new-password"
            required
            hint="At least 8 characters, with upper and lower case, a digit and a symbol."
            {...form.bind('password')}
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

export function NotFoundPage() {
  return (
    <div className="narrow">
      <PageHeader title="Page not found" subtitle="The page you were looking for doesn't exist." />
      <Link to="/">Back to booking</Link>
    </div>
  )
}
