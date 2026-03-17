import { useEffect, useState } from 'react'
import { api } from '../services/api'
import './Tasks.css'

interface Task {
  id: number
  taskId: string
  status: string
  taskType: string
  priority: number
  createdAt: string
  startedAt?: string
  completedAt?: string
  result?: string
  errorMessage?: string
  retryCount: number
  workerId?: string
}

export default function Tasks() {
  const [tasks, setTasks] = useState<Task[]>([])
  const [loading, setLoading] = useState(true)
  const [filter, setFilter] = useState<string>('all')

  useEffect(() => {
    loadTasks()
    const interval = setInterval(loadTasks, 3000)
    return () => clearInterval(interval)
  }, [filter])

  const loadTasks = async () => {
    try {
      const url = filter !== 'all' ? `/tasks?status=${filter}` : '/tasks'
      const response = await api.get(url)
      setTasks(response.data || [])
    } catch (error) {
      console.error('Failed to load tasks:', error)
    } finally {
      setLoading(false)
    }
  }

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'Completed':
        return '#4caf50'
      case 'Failed':
        return '#f44336'
      case 'Processing':
        return '#ff9800'
      case 'Pending':
        return '#2196f3'
      default:
        return '#9e9e9e'
    }
  }

  if (loading && tasks.length === 0) {
    return <div className="loading">Loading tasks...</div>
  }

  return (
    <div className="tasks-page" data-testid="tasks-page">
      <div className="page-header">
        <h1>Tasks</h1>
        <div className="filter-buttons">
          <button
            className={filter === 'all' ? 'active' : ''}
            onClick={() => setFilter('all')}
          >
            All
          </button>
          <button
            className={filter === 'Pending' ? 'active' : ''}
            onClick={() => setFilter('Pending')}
          >
            Pending
          </button>
          <button
            className={filter === 'Processing' ? 'active' : ''}
            onClick={() => setFilter('Processing')}
          >
            Processing
          </button>
          <button
            className={filter === 'Completed' ? 'active' : ''}
            onClick={() => setFilter('Completed')}
          >
            Completed
          </button>
          <button
            className={filter === 'Failed' ? 'active' : ''}
            onClick={() => setFilter('Failed')}
          >
            Failed
          </button>
        </div>
      </div>

      <div className="tasks-table-container">
        <table className="tasks-table">
          <thead>
            <tr>
              <th>Task ID</th>
              <th>Type</th>
              <th>Status</th>
              <th>Priority</th>
              <th>Created</th>
              <th>Worker</th>
              <th>Retries</th>
            </tr>
          </thead>
          <tbody>
            {tasks.length === 0 ? (
              <tr>
                <td colSpan={7} className="no-tasks">
                  No tasks found
                </td>
              </tr>
            ) : (
              tasks.map((task) => (
                <tr key={task.id}>
                  <td className="task-id">{task.taskId.substring(0, 8)}...</td>
                  <td>{task.taskType}</td>
                  <td>
                    <span
                      className="status-badge"
                      style={{ backgroundColor: getStatusColor(task.status) }}
                    >
                      {task.status}
                    </span>
                  </td>
                  <td>{task.priority}</td>
                  <td>{new Date(task.createdAt).toLocaleString()}</td>
                  <td>{task.workerId || '-'}</td>
                  <td>{task.retryCount}</td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  )
}



