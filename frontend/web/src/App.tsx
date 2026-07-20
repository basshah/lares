import { Route, Routes } from 'react-router-dom'
import ProtectedRoute from './components/ProtectedRoute'
import RequireHome from './components/RequireHome'
import Home from './pages/Home'
import HomeSetup from './pages/HomeSetup'
import MyHome from './pages/MyHome'
import Devices from './pages/Devices'
import Areas from './pages/Areas'
import Labels from './pages/Labels'
import Login from './pages/Login'
import Register from './pages/Register'

function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route path="/register" element={<Register />} />
      <Route element={<ProtectedRoute />}>
        <Route path="/home/setup" element={<HomeSetup />} />
        <Route element={<RequireHome />}>
          <Route path="/" element={<Home />} />
          <Route path="/home" element={<MyHome />} />
          <Route path="/devices" element={<Devices />} />
          <Route path="/areas" element={<Areas />} />
          <Route path="/labels" element={<Labels />} />
        </Route>
      </Route>
    </Routes>
  )
}

export default App
