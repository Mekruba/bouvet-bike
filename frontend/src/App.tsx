import { Navigate, Route, Routes } from 'react-router'
import { AuthProvider } from './auth/AuthProvider.tsx'
import { RequireRole } from './auth/RequireRole.tsx'
import { AdminLayout, SiteLayout } from './components/Layout.tsx'
import { AdminOrderPage } from './pages/admin/AdminOrderPage.tsx'
import { AdminOrdersPage } from './pages/admin/AdminOrdersPage.tsx'
import { DiscountCodesPage } from './pages/admin/DiscountCodesPage.tsx'
import { ServiceTypesPage } from './pages/admin/ServiceTypesPage.tsx'
import { StaffPage } from './pages/admin/StaffPage.tsx'
import { ConfirmEmailPage, LoginPage, NotFoundPage, RegisterPage } from './pages/AuthPages.tsx'
import { BookingPage } from './pages/BookingPage.tsx'
import { MyOrdersPage } from './pages/MyOrdersPage.tsx'
import { OrderLookupPage } from './pages/OrderLookupPage.tsx'
import { OrderPage } from './pages/OrderPage.tsx'

/**
 * Customer portal at the root, staff area under /admin. The route guards only shape the UI;
 * the API checks every request itself.
 */
function App() {
  return (
    <AuthProvider>
      <Routes>
        <Route element={<SiteLayout />}>
          <Route index element={<BookingPage />} />
          <Route path="orders/find" element={<OrderLookupPage />} />
          <Route path="orders/:reference" element={<OrderPage />} />
          <Route
            path="my-orders"
            element={
              <RequireRole roles={['Customer']}>
                <MyOrdersPage />
              </RequireRole>
            }
          />
          <Route path="login" element={<LoginPage />} />
          <Route path="register" element={<RegisterPage />} />
          <Route path="confirm-email" element={<ConfirmEmailPage />} />

          <Route
            path="admin"
            element={
              <RequireRole roles={['Admin', 'Mechanic']}>
                <AdminLayout />
              </RequireRole>
            }
          >
            <Route index element={<Navigate to="orders" replace />} />
            <Route path="orders" element={<AdminOrdersPage />} />
            <Route path="orders/:id" element={<AdminOrderPage />} />
            <Route path="service-types" element={<ServiceTypesPage />} />
            <Route
              path="discount-codes"
              element={
                <RequireRole roles={['Admin']}>
                  <DiscountCodesPage />
                </RequireRole>
              }
            />
            <Route
              path="staff"
              element={
                <RequireRole roles={['Admin']}>
                  <StaffPage />
                </RequireRole>
              }
            />
          </Route>

          <Route path="*" element={<NotFoundPage />} />
        </Route>
      </Routes>
    </AuthProvider>
  )
}

export default App
