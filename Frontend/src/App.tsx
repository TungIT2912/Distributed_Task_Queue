import { Routes, Route, Navigate } from 'react-router-dom'
import { AuthProvider, useAuth } from './contexts/AuthContext'
import Login from './pages/Login'
import Register from './pages/Register'
import Layout from './components/Layout'
import PortalRedirect from './pages/PortalRedirect'
import UserPortal from './pages/UserPortal'
import AdminPortal from './pages/AdminPortal'
import WorkerPortal from './pages/WorkerPortal'

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated } = useAuth()
  return isAuthenticated ? <>{children}</> : <Navigate to="/login" replace />
}

function App() {
  return (
    <AuthProvider>
      <Routes>
        <Route path="/login" element={<Login />} />
        <Route path="/register" element={<Register />} />
        <Route
          path="/"
          element={
            <ProtectedRoute>
              <Layout />
            </ProtectedRoute>
          }
        >
          <Route index element={<PortalRedirect />} />
          <Route path="portal" element={<PortalRedirect />} />
          <Route path="user" element={<UserPortal />} />
          <Route path="admin" element={<AdminPortal />} />
          <Route path="worker" element={<WorkerPortal />} />
        </Route>
      </Routes>
    </AuthProvider>
  )
}

export default App



