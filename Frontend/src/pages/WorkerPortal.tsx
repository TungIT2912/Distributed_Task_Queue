import Workers from './Workers'
import './Portal.css'

export default function WorkerPortal() {
  return (
    <div className="portal" data-testid="worker-portal">
      <h1>Worker Portal</h1>
      <p className="muted">
        This portal is for worker operators. It shows active workers and health.
        Worker services still run as separate processes/containers.
      </p>
      <div className="section">
        <Workers />
      </div>
    </div>
  )
}


