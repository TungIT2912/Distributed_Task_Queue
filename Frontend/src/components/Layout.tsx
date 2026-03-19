import { Outlet, Link, useLocation } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'
import './Layout.css'

export default function Layout() {
  const { user, logout } = useAuth()
  const location = useLocation()
  const role = (user?.role || 'User').toLowerCase()

  return (
    <div className="layout">
      <nav className="navbar">
        <div className="nav-brand">
          <h1>Distributed Task Queue</h1>
        </div>
        <div className="nav-links">
          {role === 'user' && (
            <Link to="/user" className={location.pathname === '/user' ? 'active' : ''}>
              User
            </Link>
          )}
          {role === 'admin' && (
            <>
              <Link to="/admin" className={location.pathname === '/admin' ? 'active' : ''}>
                Dashboard
              </Link>
              <Link to="/admin-workers" className={location.pathname === '/admin-workers' ? 'active' : ''}>
                Workers
              </Link>
              <Link to="/worker" className={location.pathname === '/worker' ? 'active' : ''}>
              Tasks
            </Link>
            </>
          )}
          {role === 'worker' && (
            <Link to="/worker" className={location.pathname === '/worker' ? 'active' : ''}>
              Worker
            </Link>
          )}
        </div>
        <div className="nav-user">
          <span>Welcome, {user?.username}</span>
          <button onClick={logout} className="logout-btn">
            Logout
          </button>
        </div>
      </nav>
      <main className="main-content">
        <Outlet />
      </main>
    </div>
  )
}