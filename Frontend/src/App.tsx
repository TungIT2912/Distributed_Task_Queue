import { Routes, Route, Navigate } from 'react-router-dom'
import { AuthProvider, useAuth } from './contexts/AuthContext'
import Login from './pages/Login'
import Register from './pages/Register'
import Layout from './components/Layout'
import PortalRedirect from './pages/PortalRedirect'
import UserPortal from './pages/UserPortal'
import AdminPortal from './pages/AdminPortal'
import WorkerPortal from './pages/WorkerPortal'
import Workers from './pages/Workers'

function Spinner() {
  return (
    <div style={{
      minHeight: '100vh', display: 'flex', alignItems: 'center',
      justifyContent: 'center',
      background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)',
      color: 'white', fontSize: '1.2rem',
    }}>
      Loading...
    </div>
  )
}

function ProtectedRoute({
  children,
  allowedRoles,
}: {
  children: React.ReactNode
  allowedRoles?: string[]
}) {
  const { isAuthenticated, user, loading } = useAuth()
  if (loading) return <Spinner />
  if (!isAuthenticated) return <Navigate to="/login" replace />
  if (allowedRoles && user && !allowedRoles.includes(user.role)) {
    const role = user.role.toLowerCase()
    if (role === 'admin') return <Navigate to="/admin" replace />
    if (role === 'worker') return <Navigate to="/worker" replace />
    return <Navigate to="/user" replace />
  }
  return <>{children}</>
}

function App() {
  return (
    <AuthProvider>
      <Routes>
        <Route path="/login" element={<Login />} />
        <Route path="/register" element={<Register />} />

        <Route path="/" element={<ProtectedRoute><Layout /></ProtectedRoute>}>
          <Route index element={<PortalRedirect />} />
          <Route path="portal" element={<PortalRedirect />} />

          <Route path="user" element={
            <ProtectedRoute allowedRoles={['User']}><UserPortal /></ProtectedRoute>
          } />

          <Route path="admin" element={
            <ProtectedRoute allowedRoles={['Admin']}><AdminPortal /></ProtectedRoute>
          } />

          <Route path="admin-workers" element={
            <ProtectedRoute allowedRoles={['Admin']}><Workers /></ProtectedRoute>
          } />

          <Route path="worker" element={
            <ProtectedRoute allowedRoles={['Worker','Admin']}><WorkerPortal /></ProtectedRoute>
          } />
        </Route>

        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </AuthProvider>
  )
}

export default App