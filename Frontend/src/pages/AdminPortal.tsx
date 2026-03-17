import Dashboard from './Dashboard'
import Tasks from './Tasks'
import Workers from './Workers'
import './Portal.css'

export default function AdminPortal() {
  return (
    <div className="portal" data-testid="admin-portal">
      <h1>Admin Portal</h1>
      <div className="stack">
        <div className="section">
          <Dashboard />
        </div>
        <div className="section">
          <Tasks />
        </div>
        <div className="section">
          <Workers />
        </div>
      </div>
    </div>
  )
}


