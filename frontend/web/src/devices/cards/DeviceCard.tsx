import type { Device } from '../types'
import LightCard from './LightCard'
import SocketCard from './SocketCard'
import ThermostatCard from './ThermostatCard'
import CameraCard from './CameraCard'
import TvCard from './TvCard'

export default function DeviceCard({ device }: { device: Device }) {
  switch (device.type) {
    case 'Light':
      return <LightCard device={device} />
    case 'Socket':
      return <SocketCard device={device} />
    case 'Thermostat':
      return <ThermostatCard device={device} />
    case 'Camera':
      return <CameraCard device={device} />
    case 'Tv':
      return <TvCard device={device} />
  }
}
