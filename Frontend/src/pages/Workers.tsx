import { useEffect, useState } from 'react'
import { api } from '../services/api'
import './Workers.css'

interface Worker {
  id: number
  workerId: string
  status: string
  hostAddress?: string
  port: number
  registeredAt: string
  lastHeartbeat?: string
  tasksProcessed: number
  tasksFailed: number
}

export default function Workers() {
  const [workers, setWorkers] = useState<Worker[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    loadWorkers()
    const interval = setInterval(loadWorkers, 5000)
    return () => clearInterval(interval)
  }, [])

  const loadWorkers = async () => {
    try {
      const response = await api.get('/workers/peers')
      setWorkers(response.data || [])
    } catch (error) {
      console.error('Failed to load workers:', error)
    } finally {
      setLoading(false)
    }
  }

  const getStatusColor = (status: string) => {
    return status === 'Active' ? '#4caf50' : '#9e9e9e'
  }

  const getUptime = (lastHeartbeat?: string) => {
    if (!lastHeartbeat) return 'N/A'
    const diff = Date.now() - new Date(lastHeartbeat).getTime()
    const minutes = Math.floor(diff / 60000)
    const hours = Math.floor(minutes / 60)
    if (hours > 0) return `${hours}h ${minutes % 60}m`
    return `${minutes}m`
  }

  if (loading && workers.length === 0) {
    return <div className="loading">Loading workers...</div>
  }

  return (
    <div className="workers-page" data-testid="workers-page">
      <h1>Workers</h1>
      <div className="workers-grid">
        {workers.length === 0 ? (
          <div className="no-workers">No active workers found</div>
        ) : (
          workers.map((worker) => (
            <div key={worker.id} className="worker-card">
              <div className="worker-header">
                <h3>{worker.workerId}</h3>
                <span
                  className="status-badge"
                  style={{ backgroundColor: getStatusColor(worker.status) }}
                >
                  {worker.status}
                </span>
              </div>
              <div className="worker-info">
                <div className="info-row">
                  <span className="info-label">Address:</span>
                  <span className="info-value">
                    {worker.hostAddress || 'N/A'}:{worker.port || 'N/A'}
                  </span>
                </div>
                <div className="info-row">
                  <span className="info-label">Registered:</span>
                  <span className="info-value">
                    {new Date(worker.registeredAt).toLocaleString()}
                  </span>
                </div>
                <div className="info-row">
                  <span className="info-label">Last Heartbeat:</span>
                  <span className="info-value">{getUptime(worker.lastHeartbeat)} ago</span>
                </div>
                <div className="worker-stats">
                  <div className="stat">
                    <span className="stat-label">Processed</span>
                    <span className="stat-value success">{worker.tasksProcessed}</span>
                  </div>
                  <div className="stat">
                    <span className="stat-label">Failed</span>
                    <span className="stat-value error">{worker.tasksFailed}</span>
                  </div>
                </div>
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  )
}



