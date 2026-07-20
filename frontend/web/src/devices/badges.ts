import type { Device, DeviceType } from './types'

export function badgeIcon(type: DeviceType): string {
  switch (type) {
    case 'Light':
      return '💡'
    case 'Socket':
      return '🔌'
    case 'Thermostat':
      return '🌡️'
    case 'Camera':
      return '📷'
    case 'Tv':
      return '📺'
  }
}

export function badgeValue(device: Device, t: (key: string) => string): string {
  switch (device.type) {
    case 'Light':
      return device.attributes.light?.isOn
        ? `${device.attributes.light.brightness ?? 100}%`
        : t('dashboard.state.off')
    case 'Socket':
      return device.attributes.socket?.isOn ? t('dashboard.state.on') : t('dashboard.state.off')
    case 'Thermostat': {
      const temp =
        device.attributes.thermostat?.currentTemperatureC ?? device.attributes.thermostat?.targetTemperatureC
      return temp != null ? `${temp}°C` : '—'
    }
    case 'Camera':
      return device.attributes.camera?.isOnline ? t('dashboard.state.online') : t('dashboard.state.offline')
    case 'Tv':
      return device.attributes.tv?.isOn ? `${device.attributes.tv.volume}%` : t('dashboard.state.off')
  }
}
