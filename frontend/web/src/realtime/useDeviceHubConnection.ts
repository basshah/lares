import { useEffect } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { HubConnectionBuilder } from '@microsoft/signalr'
import { tokenStorage } from '../auth/storage'
import { devicesQueryKey } from '../devices/useDevices'
import { areasQueryKey } from '../areas/useAreas'
import { labelsQueryKey } from '../labels/useLabels'

export function useDeviceHubConnection(enabled: boolean) {
  const queryClient = useQueryClient()

  useEffect(() => {
    if (!enabled) return

    const connection = new HubConnectionBuilder()
      .withUrl(`${import.meta.env.VITE_API_URL}/hubs/devices`, {
        accessTokenFactory: () => tokenStorage.getAccessToken() ?? '',
      })
      .withAutomaticReconnect()
      .build()

    connection.on('homeChanged', () => {
      queryClient.invalidateQueries({ queryKey: devicesQueryKey })
      queryClient.invalidateQueries({ queryKey: areasQueryKey })
      queryClient.invalidateQueries({ queryKey: labelsQueryKey })
    })

    connection.start().catch((err: unknown) => console.error('SignalR connection failed', err))

    return () => {
      void connection.stop()
    }
  }, [enabled, queryClient])
}
