import { NavLink, Route, Routes } from 'react-router'
import AdminPage from './pages/AdminPage.tsx'
import CustomerPage from './pages/CustomerPage.tsx'

function App() {
  return (
    <>
      <header className="app-header">
        <strong>BouvetBike</strong>
        <nav>
          <NavLink to="/" end>
            Customer
          </NavLink>
          <NavLink to="/admin">Admin</NavLink>
        </nav>
      </header>
      <main>
        <Routes>
          <Route path="/" element={<CustomerPage />} />
          <Route path="/admin" element={<AdminPage />} />
        </Routes>
      </main>
    </>
  )
}

export default App
