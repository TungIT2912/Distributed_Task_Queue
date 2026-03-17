import { useEffect, useState } from 'react'
import { api } from '../services/api'
import './Dashboard.css'

interface DashboardStats {
  activeWorkers: number
  totalTasks: number
  completedTasks: number
  pendingTasks: number
  failedTasks: number
}

export default function Dashboard() {
  const [stats, setStats] = useState<DashboardStats>({
    activeWorkers: 0,
    totalTasks: 0,
    completedTasks: 0,
    pendingTasks: 0,
    failedTasks: 0,
  })
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    loadStats()
    const interval = setInterval(loadStats, 5000)
    return () => clearInterval(interval)
  }, [])

  const loadStats = async () => {
    try {
      const [workersRes, tasksRes] = await Promise.all([
        api.get('/workers/peers'),
        api.get('/tasks?limit=1000'),
      ])

      const workers = workersRes.data || []
      const tasks = tasksRes.data || []

      setStats({
        activeWorkers: workers.length,
        totalTasks: tasks.length,
        completedTasks: tasks.filter((t: any) => t.status === 'Completed').length,
        pendingTasks: tasks.filter((t: any) => t.status === 'Pending' || t.status === 'Processing').length,
        failedTasks: tasks.filter((t: any) => t.status === 'Failed').length,
      })
    } catch (error) {
      console.error('Failed to load stats:', error)
    } finally {
      setLoading(false)
    }
  }

  if (loading) {
    return <div className="loading">Loading dashboard...</div>
  }

  return (
    <div className="dashboard" data-testid="dashboard">
      <h1>Dashboard</h1>
      <div className="stats-grid">
        <div className="stat-card">
          <div className="stat-icon">👷</div>
          <div className="stat-info">
            <h3>Active Workers</h3>
            <p className="stat-value">{stats.activeWorkers}</p>
          </div>
        </div>
        <div className="stat-card">
          <div className="stat-icon">📋</div>
          <div className="stat-info">
            <h3>Total Tasks</h3>
            <p className="stat-value">{stats.totalTasks}</p>
          </div>
        </div>
        <div className="stat-card">
          <div className="stat-icon">✅</div>
          <div className="stat-info">
            <h3>Completed</h3>
            <p className="stat-value">{stats.completedTasks}</p>
          </div>
        </div>
        <div className="stat-card">
          <div className="stat-icon">⏳</div>
          <div className="stat-info">
            <h3>Pending</h3>
            <p className="stat-value">{stats.pendingTasks}</p>
          </div>
        </div>
        <div className="stat-card">
          <div className="stat-icon">❌</div>
          <div className="stat-info">
            <h3>Failed</h3>
            <p className="stat-value">{stats.failedTasks}</p>
          </div>
        </div>
      </div>
    </div>
  )
}



