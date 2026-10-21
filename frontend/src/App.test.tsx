import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App.tsx'

/** Smoke tests: the app renders its main routes. The backend is covered by its own API tests. */
describe('App', () => {
  beforeEach(() => {
    // No backend here: every API call answers 401, i.e. logged out with no data.
    vi.stubGlobal('fetch', vi.fn(async () => new Response(null, { status: 401 })))
  })

  const renderAt = (path: string) =>
    render(
      <MemoryRouter initialEntries={[path]}>
        <App />
      </MemoryRouter>,
    )

  it('shows the booking page at /', async () => {
    renderAt('/')
    expect(await screen.findByRole('heading', { name: 'Book a service in two minutes' })).toBeInTheDocument()
  })

  it('sends anonymous visitors from the staff area to the login page', async () => {
    renderAt('/admin/orders')
    expect(await screen.findByRole('heading', { name: 'Log in' })).toBeInTheDocument()
  })
})
