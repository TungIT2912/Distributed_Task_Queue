import { Navigate } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'

export default function PortalRedirect() {
  const { user, loading, isAuthenticated } = useAuth()

  if (loading) return null 

  if (!isAuthenticated) return <Navigate to="/login" replace />

  const role = user?.role?.toLowerCase()
  if (role === 'admin') return <Navigate to="/admin" replace />
  if (role === 'worker') return <Navigate to="/worker" replace />
  return <Navigate to="/user" replace />
}