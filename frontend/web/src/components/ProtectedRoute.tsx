import { Navigate, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

export default function ProtectedRoute() {
  const { user, isInitializing } = useAuth()

  if (isInitializing) {
    return <div className="min-h-screen bg-slate-950" />
  }

  return user ? <Outlet /> : <Navigate to="/login" replace />
}
