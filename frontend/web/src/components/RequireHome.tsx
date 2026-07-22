import { Navigate, Outlet } from 'react-router-dom'
import { useMyHome } from '../home/useHome'
import { useDeviceHubConnection } from '../realtime/useDeviceHubConnection'

export default function RequireHome() {
  const { data: home, isLoading } = useMyHome()
  useDeviceHubConnection(Boolean(home))

  if (isLoading) {
    return <div className="min-h-screen bg-slate-950" />
  }

  return home ? <Outlet /> : <Navigate to="/home/setup" replace />
}
