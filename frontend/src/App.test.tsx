import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { describe, expect, it } from 'vitest'
import App from './App.tsx'

function renderAt(path: string) {
  render(
    <MemoryRouter initialEntries={[path]}>
      <App />
    </MemoryRouter>,
  )
}

describe('App routing', () => {
  it('shows the customer portal at /', () => {
    renderAt('/')
    expect(screen.getByRole('heading', { name: 'Customer portal' })).toBeInTheDocument()
  })

  it('shows the admin page at /admin', () => {
    renderAt('/admin')
    expect(screen.getByRole('heading', { name: 'Admin' })).toBeInTheDocument()
  })
})
