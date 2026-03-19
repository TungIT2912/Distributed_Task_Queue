import { useEffect, useRef, useState } from 'react'
import { api } from '../services/api'
import { useAuth } from '../contexts/AuthContext'
import './Portal.css'

interface Task {
  id: number
  taskId: string
  status: string
  taskType: string
  priority: number
  payload: string
  createdAt: string
  result?: string
  errorMessage?: string
  retryCount: number
  workerId?: string
}

function executeTask(taskType: string, payloadStr: string): { result: string; error?: string } {
  try {
    const payload = JSON.parse(payloadStr || '{}')

    switch (taskType) {
      case 'Compute': {
        const { operation, a, b } = payload
        let result: number
        switch (operation) {
          case 'add':      result = a + b; break
          case 'subtract': result = a - b; break
          case 'multiply': result = a * b; break
          case 'divide':
            if (b === 0) return { result: '', error: 'Division by zero' }
            result = a / b; break
          default:
            return { result: '', error: `Unknown operation: ${operation}` }
        }
        return { result: JSON.stringify({ operation, a, b, result }) }
      }

      case 'DataProcessing': {
        const { inputData, action } = payload
        if (!Array.isArray(inputData)) return { result: '', error: 'inputData must be an array' }
        let result: any
        switch (action) {
          case 'sum':     result = inputData.reduce((acc: number, v: number) => acc + v, 0); break
          case 'average': result = inputData.reduce((acc: number, v: number) => acc + v, 0) / inputData.length; break
          case 'max':     result = Math.max(...inputData); break
          case 'min':     result = Math.min(...inputData); break
          case 'sort':    result = [...inputData].sort((a, b) => a - b); break
          case 'count':   result = inputData.length; break
          default:
            return { result: '', error: `Unknown action: ${action}` }
        }
        return { result: JSON.stringify({ action, inputData, result }) }
      }

      case 'Email': {
        const { to, subject, body } = payload
        if (!to) return { result: '', error: 'Missing "to" field' }
        return {
          result: JSON.stringify({
            sent: true,
            to,
            subject: subject || '(no subject)',
            body: body || '',
            sentAt: new Date().toISOString(),
            messageId: `msg-${Math.random().toString(36).slice(2, 10)}`
          })
        }
      }

      default: {
        return {
          result: JSON.stringify({
            processed: true,
            taskType,
            payload,
            processedAt: new Date().toISOString()
          })
        }
      }
    }
  } catch (e: any) {
    return { result: '', error: `Failed to parse payload: ${e.message}` }
  }
}

export default function WorkerPortal() {
  const { user } = useAuth()
  const [pendingTasks, setPendingTasks] = useState<Task[]>([])
  const [myTasks, setMyTasks] = useState<Task[]>([])
  const [loading, setLoading] = useState(true)
  const [claimingId, setClaimingId] = useState<string | null>(null)
  const [completingId, setCompletingId] = useState<string | null>(null)
  const [activeTab, setActiveTab] = useState<'available' | 'mine'>('available')
  const [taskResults, setTaskResults] = useState<Record<string, string>>({})

  const workerIdRef = useRef<string>(
    localStorage.getItem('workerId') || `worker-${user?.username}`
  )
  const workerId = workerIdRef.current

  const taskHeartbeats = useRef<Record<string, ReturnType<typeof setInterval>>>({})
  const loginHeartbeat = useRef<ReturnType<typeof setInterval> | null>(null)

  const ping = async () => {
    try { await api.get(`/workers/heartbeat/${workerId}`) } catch { }
  }

  const startLoginHeartbeat = () => {
    if (loginHeartbeat.current) return
    ping()
    loginHeartbeat.current = setInterval(ping, 30000)
  }

  const stopLoginHeartbeat = () => {
    if (loginHeartbeat.current) { clearInterval(loginHeartbeat.current); loginHeartbeat.current = null }
  }

  const startTaskHeartbeat = (taskId: string) => {
    if (taskHeartbeats.current[taskId]) return
    ping()
    taskHeartbeats.current[taskId] = setInterval(ping, 15000)
  }

  const stopTaskHeartbeat = (taskId: string) => {
    if (taskHeartbeats.current[taskId]) { clearInterval(taskHeartbeats.current[taskId]); delete taskHeartbeats.current[taskId] }
  }

  const stopAllTaskHeartbeats = () => Object.keys(taskHeartbeats.current).forEach(stopTaskHeartbeat)

  const loadData = async () => {
    try {
      const res = await api.get('/tasks?limit=200')
      const allTasks: Task[] = res.data || []
      const myProcessing = allTasks.filter(t => t.workerId === workerId && t.status === 'Processing')
      setPendingTasks(allTasks.filter(t => t.status === 'Pending'))
      setMyTasks(allTasks.filter(t => t.workerId === workerId))
      myProcessing.forEach(t => startTaskHeartbeat(t.taskId))
      Object.keys(taskHeartbeats.current).forEach(taskId => {
        if (!myProcessing.some(t => t.taskId === taskId)) stopTaskHeartbeat(taskId)
      })
    } catch (err) {
      console.error('Failed to load tasks:', err)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    startLoginHeartbeat()
    loadData()
    const dataInterval = setInterval(loadData, 4000)
    return () => { clearInterval(dataInterval); stopLoginHeartbeat(); stopAllTaskHeartbeats() }
  }, [])

  const claimTask = async (task: Task) => {
    setClaimingId(task.taskId)
    try {
      await api.put(`/tasks/${task.taskId}/status`, { status: 'Processing', workerKey: workerId })
      startTaskHeartbeat(task.taskId)
      console.log('payload:', task.payload, 'type:', task.taskType)
      const { result, error } = executeTask(task.taskType, task.payload ?? '{}')
      setTaskResults(prev => ({
        ...prev,
        [task.taskId]: error ? `ERROR: ${error}` : result
      }))
      await loadData()
    } catch (err: any) {
      alert(err.response?.data?.message || 'Failed to claim task')
    } finally {
      setClaimingId(null)
    }
  }

  const completeTask = async (task: Task, success: boolean) => {
    setCompletingId(task.taskId)
    try {
      const result = taskResults[task.taskId] || `Completed by ${workerId}`
      await api.put(`/tasks/${task.taskId}/status`, {
        status: success ? 'Completed' : 'Failed',
        workerKey: workerId,
        result: success ? result : null,
        errorMessage: success ? null : 'Marked as failed by worker',
      })
      stopTaskHeartbeat(task.taskId)
      setTaskResults(prev => { const n = { ...prev }; delete n[task.taskId]; return n })
      await loadData()
    } catch (err: any) {
      alert(err.response?.data?.message || 'Failed to update task')
    } finally {
      setCompletingId(null)
    }
  }

  const processingTasks = myTasks.filter(t => t.status === 'Processing')

  return (
    <div style={{ color: 'white' }}>
      {/* Header */}
      <div style={{ marginBottom: '1.5rem' }}>
        <h1 style={{ fontSize: '2.5rem', marginBottom: '0.25rem' }}>Worker Portal</h1>
        <p style={{ opacity: 0.85, margin: 0, display: 'flex', alignItems: 'center', gap: '0.75rem', flexWrap: 'wrap' }}>
          Logged in as <strong>{user?.username}</strong> · Worker ID:{' '}
          <code style={{ background: 'rgba(0,0,0,0.15)', padding: '0.1rem 0.4rem', borderRadius: '4px' }}>{workerId}</code>
          <span style={{ background: '#4caf50', color: 'white', padding: '0.15rem 0.6rem', borderRadius: '999px', fontSize: '0.78rem', fontWeight: 700 }}>● Active</span>
          {processingTasks.length > 0 && (
            <span style={{ background: '#ff9800', color: 'white', padding: '0.15rem 0.6rem', borderRadius: '999px', fontSize: '0.78rem', fontWeight: 700 }}>
              ⚡ Processing {processingTasks.length} task{processingTasks.length > 1 ? 's' : ''}
            </span>
          )}
        </p>
      </div>
      {processingTasks.length > 0 && (
        <div className="card" style={{ borderLeft: '4px solid #ff9800', marginBottom: '1rem' }}>
          <h2 style={{ marginBottom: '1rem', color: '#333' }}>⚙️ Currently Processing ({processingTasks.length})</h2>
          <div className="list">
            {processingTasks.map(task => {
              const previewResult = taskResults[task.taskId]
              let parsedResult: any = null
              try { if (previewResult) parsedResult = JSON.parse(previewResult) } catch { }

              return (
                <div key={task.id} className="list-item">
                  <div className="row">
                    <div>
                      <span className="mono">{task.taskId.slice(0, 8)}…</span>
                      <span className="badge processing ml">Processing</span>
                      <span style={{ marginLeft: '0.5rem', color: '#888', fontSize: '0.82rem' }}>
                        {task.taskType} · Priority {task.priority}
                      </span>
                    </div>
                    <div className="action-group">
                      <button className="btn-success" disabled={completingId === task.taskId} onClick={() => completeTask(task, true)}>
                        {completingId === task.taskId ? '...' : '✓ Complete'}
                      </button>
                      <button className="btn-danger" disabled={completingId === task.taskId} onClick={() => completeTask(task, false)}>
                        ✗ Fail
                      </button>
                    </div>
                  </div>

                  <div style={{ marginTop: '0.75rem' }}>
                    <div style={{ fontSize: '0.78rem', fontWeight: 600, color: '#999', marginBottom: '0.25rem' }}>INPUT PAYLOAD</div>
                    <div className="payload-preview" style={{ whiteSpace: 'pre-wrap', maxHeight: '80px', overflow: 'auto' }}>
                      {(task?.payload ?? '')}
                    </div>
                  </div>

                  <div style={{ marginTop: '0.75rem' }}>
                    <div style={{ fontSize: '0.78rem', fontWeight: 600, color: '#999', marginBottom: '0.25rem' }}>
                      RESULT <span style={{ color: '#4caf50' }}>(auto-computed — edit if needed)</span>
                    </div>
                    <textarea
                      rows={3}
                      value={taskResults[task.taskId] ?? ''}
                      onChange={e => setTaskResults(prev => ({ ...prev, [task.taskId]: e.target.value }))}
                      style={{
                        width: '100%', fontFamily: 'monospace', fontSize: '0.82rem',
                        border: '2px solid #e0e0e0', borderRadius: '6px', padding: '0.5rem',
                        resize: 'vertical', color: '#333', background: '#f9f9f9'
                      }}
                      placeholder="Result will appear here after claiming..."
                    />
                    {parsedResult && (
                      <div style={{ marginTop: '0.5rem', background: '#f0fff4', border: '1px solid #b7ebc0', borderRadius: '6px', padding: '0.5rem' }}>
                        <div style={{ fontSize: '0.78rem', fontWeight: 600, color: '#2e7d32', marginBottom: '0.25rem' }}>COMPUTED OUTPUT</div>
                        {Object.entries(parsedResult).map(([k, v]) => (
                          <div key={k} style={{ fontSize: '0.85rem', color: '#333' }}>
                            <strong>{k}:</strong> {JSON.stringify(v)}
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                </div>
              )
            })}
          </div>
        </div>
      )}

      <div className="card" style={{ padding: 0 }}>
        <div style={{ display: 'flex', borderBottom: '2px solid #e0e0e0' }}>
          {(['available', 'mine'] as const).map(tab => (
            <button key={tab} onClick={() => setActiveTab(tab)} style={{
              padding: '1rem 1.5rem', border: 'none', background: 'none', cursor: 'pointer',
              fontSize: '0.95rem', fontWeight: activeTab === tab ? 700 : 400,
              color: activeTab === tab ? '#667eea' : '#666',
              borderBottom: activeTab === tab ? '3px solid #667eea' : '3px solid transparent',
              marginBottom: '-2px', display: 'flex', alignItems: 'center', gap: '0.4rem',
            }}>
              {tab === 'available' ? 'Available Tasks' : 'My History'}
              {tab === 'available' && pendingTasks.length > 0 && (
                <span style={{ background: '#667eea', color: 'white', borderRadius: '10px', padding: '0.1rem 0.5rem', fontSize: '0.75rem' }}>{pendingTasks.length}</span>
              )}
              {tab === 'mine' && myTasks.length > 0 && (
                <span style={{ background: '#667eea', color: 'white', borderRadius: '10px', padding: '0.1rem 0.5rem', fontSize: '0.75rem' }}>{myTasks.length}</span>
              )}
            </button>
          ))}
        </div>

        <div style={{ padding: '1.5rem' }}>
          {loading ? (
            <div style={{ color: '#888', textAlign: 'center', padding: '2rem' }}>Loading...</div>
          ) : activeTab === 'available' ? (
            <div>
              <h2 style={{ marginBottom: '1rem', color: '#333' }}>Pending Tasks</h2>
              <div className="list">
                {pendingTasks.length === 0 ? (
                  <div style={{ color: '#888', textAlign: 'center', padding: '2rem' }}>No pending tasks — check back soon.</div>
                ) : (
                  [...pendingTasks].sort((a, b) => b.priority - a.priority).map(task => (
                    <div key={task.id} className="list-item">
                      <div className="row">
                        <div>
                          <span className="mono">{task.taskId.slice(0, 8)}…</span>
                          <span className="badge pending ml">Pending</span>
                          {task.priority >= 7 && <span className="badge high ml">HIGH</span>}
                        </div>
                        <button className="btn-primary" disabled={claimingId === task.taskId} onClick={() => claimTask(task)}>
                          {claimingId === task.taskId ? 'Claiming...' : '⚡ Claim'}
                        </button>
                      </div>
                      <div style={{ color: '#666', fontSize: '0.85rem', marginTop: '0.25rem' }}>
                        {task.taskType} · Priority {task.priority} · {new Date(task.createdAt).toLocaleString()}
                      </div>
                      <div className="payload-preview">{(task?.payload ?? '').slice(0, 120)}</div>
                      {task.retryCount > 0 && <div className="retry-note">⚠️ Retried {task.retryCount}x</div>}
                    </div>
                  ))
                )}
              </div>
            </div>
          ) : (
            <div>
              <h2 style={{ marginBottom: '1rem', color: '#333' }}>My Task History</h2>
              <div className="list">
                {myTasks.length === 0 ? (
                  <div style={{ color: '#888', textAlign: 'center', padding: '2rem' }}>You haven't claimed any tasks yet.</div>
                ) : (
                  myTasks.map(task => {
                    let parsedResult: any = null
                    try { if (task.result) parsedResult = JSON.parse(task.result) } catch { }
                    return (
                      <div key={task.id} className="list-item">
                        <div className="row">
                          <span className="mono">{task.taskId.slice(0, 8)}…</span>
                          <span className={`badge ${task.status.toLowerCase()}`}>{task.status}</span>
                        </div>
                        <div style={{ color: '#666', fontSize: '0.85rem', marginTop: '0.25rem' }}>
                          {task.taskType} · Priority {task.priority} · {new Date(task.createdAt).toLocaleString()}
                        </div>
                        {parsedResult && (
                          <div style={{ marginTop: '0.5rem', background: '#f0fff4', border: '1px solid #b7ebc0', borderRadius: '6px', padding: '0.5rem' }}>
                            {Object.entries(parsedResult).map(([k, v]) => (
                              <div key={k} style={{ fontSize: '0.82rem', color: '#333' }}>
                                <strong>{k}:</strong> {JSON.stringify(v)}
                              </div>
                            ))}
                          </div>
                        )}
                        {task.result && !parsedResult && <div className="result-note">✅ {task.result}</div>}
                        {task.errorMessage && <div className="error">{task.errorMessage}</div>}
                      </div>
                    )
                  })
                )}
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}