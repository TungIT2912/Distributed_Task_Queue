import { Navigate } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'

export default function PortalRedirect() {
  const { user } = useAuth()

  const role = (user?.role || 'User').toLowerCase()
  if (role === 'admin') return <Navigate to="/admin" replace />
  if (role === 'worker') return <Navigate to="/worker" replace />
  return <Navigate to="/user" replace />
}


