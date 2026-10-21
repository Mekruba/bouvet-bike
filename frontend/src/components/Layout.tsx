import { useState } from 'react'
import { Link, NavLink, Outlet, useNavigate } from 'react-router'
import { useAuth } from '../auth/useAuth.ts'
import { initials } from '../lib/format.ts'
import { Button, ButtonLink } from './Button.tsx'
import { LogoMark } from './Logo.tsx'

/** Header, page content and footer for every page. */
export function SiteLayout() {
  const { user, hasRole, logout } = useAuth()
  const navigate = useNavigate()
  const [loggingOut, setLoggingOut] = useState(false)

  const handleLogout = async () => {
    setLoggingOut(true)
    try {
      await logout()
      navigate('/')
    } finally {
      setLoggingOut(false)
    }
  }

  return (
    <div className="app">
      <header className="site-header">
        <div className="container site-header__inner">
          <Link to="/" className="brand">
            <LogoMark className="brand__mark" />
            BouvetBike
          </Link>
          <nav className="nav" aria-label="Main">
            <NavLink to="/" end className="nav__link">
              Book a service
            </NavLink>
            <NavLink to="/orders/find" className="nav__link">
              Find my order
            </NavLink>
            {hasRole('Customer') && (
              <NavLink to="/my-orders" className="nav__link">
                My orders
              </NavLink>
            )}
            {user?.isStaff && (
              <NavLink to="/admin" className="nav__link">
                Staff area
              </NavLink>
            )}
          </nav>
          <div className="site-header__user">
            {user ? (
              <>
                <span className="user-chip" title={user.email}>
                  <span className="avatar" aria-hidden>
                    {initials(user.name)}
                  </span>
                  <span className="user-chip__name">{user.name}</span>
                </span>
                <Button variant="ghost" size="sm" loading={loggingOut} onClick={handleLogout}>
                  Log out
                </Button>
              </>
            ) : (
              <>
                <ButtonLink to="/login" variant="ghost" size="sm">
                  Log in
                </ButtonLink>
                <ButtonLink to="/register" variant="secondary" size="sm">
                  Create account
                </ButtonLink>
              </>
            )}
          </div>
        </div>
      </header>
      <main className="site-main">
        <div className="container">
          <Outlet />
        </div>
      </main>
      <footer className="site-footer">
        <div className="container cluster cluster--between">
          <span>BouvetBike: bike maintenance, booked online.</span>
          <Link to="/orders/find">Find my order</Link>
        </div>
      </footer>
    </div>
  )
}

/** Sub navigation for the staff area. Admin-only sections are hidden from mechanics. */
export function AdminLayout() {
  const { hasRole } = useAuth()
  return (
    <>
      <nav className="subnav" aria-label="Staff area">
        <NavLink to="/admin/orders" className="subnav__link">
          Orders
        </NavLink>
        <NavLink to="/admin/service-types" className="subnav__link">
          Services
        </NavLink>
        {hasRole('Admin') && (
          <>
            <NavLink to="/admin/discount-codes" className="subnav__link">
              Discount codes
            </NavLink>
            <NavLink to="/admin/staff" className="subnav__link">
              Staff
            </NavLink>
          </>
        )}
      </nav>
      <Outlet />
    </>
  )
}
