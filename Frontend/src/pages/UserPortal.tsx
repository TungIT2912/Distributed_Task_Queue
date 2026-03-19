import { useEffect, useState } from 'react'
import { api } from '../services/api'
import './Portal.css'

interface Task {
  id: number
  taskId: string
  status: string
  taskType: string
  priority: number
  createdAt: string
  workerId?: string
  retryCount: number
  result?: string
  errorMessage?: string
  payload?: string
}

const payloadSamples: Record<string, string> = {
  Compute: JSON.stringify({ operation: 'add', a: 5, b: 3 }, null, 2),
  DataProcessing: JSON.stringify({ inputData: [1, 2, 3], action: 'sum' }, null, 2),
  Email: JSON.stringify({ to: 'user@example.com', subject: 'Hello', body: 'Message here' }, null, 2),
  Default: JSON.stringify({ data: 'your data here' }, null, 2),
}

const statusColors: Record<string, string> = {
  pending: '#2196f3',
  processing: '#ff9800',
  completed: '#4caf50',
  failed: '#f44336',
  reassigned: '#9c27b0',
}

const PAGE_SIZE = 8

export default function UserPortal() {
  const [taskType, setTaskType] = useState('Default')
  const [priority, setPriority] = useState(0)
  const [payload, setPayload] = useState(payloadSamples['Default'])
  const [submitting, setSubmitting] = useState(false)
  const [submitMsg, setSubmitMsg] = useState<string | null>(null)
  const [tasks, setTasks] = useState<Task[]>([])
  const [loading, setLoading] = useState(true)
  const [selectedTask, setSelectedTask] = useState<Task | null>(null)
  const [currentPage, setCurrentPage] = useState(1)

  const handleTaskTypeChange = (newType: string) => {
    setTaskType(newType)
    setPayload(payloadSamples[newType] || '{}')
  }

  const loadTasks = async () => {
    const res = await api.get('/tasks?limit=1000')
    setTasks(res.data || [])
    setLoading(false)
  }

  useEffect(() => {
    loadTasks()
    const t = setInterval(loadTasks, 3000)
    return () => clearInterval(t)
  }, [])

  const submitTask = async (e: React.FormEvent) => {
    e.preventDefault()
    setSubmitting(true)
    setSubmitMsg(null)
    try {
      JSON.parse(payload)
    } catch {
      setSubmitMsg('Invalid JSON payload!')
      setSubmitting(false)
      return
    }
    try {
      const res = await api.post('/tasks/submit', { taskType, priority, payload })
      setSubmitMsg(`✅ Submitted task ${res.data.taskId.slice(0, 8)}...`)
      await loadTasks()
      setCurrentPage(1)
    } catch (err: any) {
      setSubmitMsg(err.response?.data?.message || 'Failed to submit task')
    } finally {
      setSubmitting(false)
    }
  }

  const parseResult = (result?: string) => {
    if (!result) return null
    try { return JSON.parse(result) } catch { return null }
  }

  const totalPages = Math.max(1, Math.ceil(tasks.length / PAGE_SIZE))
  const paginatedTasks = tasks.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE)

  const goToPage = (page: number) => {
    setCurrentPage(Math.min(Math.max(1, page), totalPages))
    setSelectedTask(null)
  }

  return (
    <div className="portal" data-testid="user-portal">
      <h1>User Portal</h1>

      <div className="portal-grid">
        <div className="card">
          <h2>Submit Task</h2>
          <form onSubmit={submitTask} className="form">
            {submitMsg && <div className="note">{submitMsg}</div>}
            <label>
              Task Type
              <select value={taskType} onChange={(e) => handleTaskTypeChange(e.target.value)}>
                <option value="Compute">Compute</option>
                <option value="DataProcessing">DataProcessing</option>
                <option value="Email">Email</option>
                <option value="Default">Default</option>
              </select>
            </label>
            <label>
              Priority (0-10)
              <input
                type="number" min={0} max={10} value={priority}
                onChange={(e) => setPriority(parseInt(e.target.value || '0', 10))}
              />
            </label>
            <label>
              Payload (JSON)
              <textarea value={payload} onChange={(e) => setPayload(e.target.value)} rows={6} />
            </label>
            <button type="submit" disabled={submitting}>
              {submitting ? 'Submitting...' : 'Submit Task'}
            </button>
          </form>
        </div>

        <div className="card">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
            <h2 style={{ margin: 0 }}>My Recent Tasks</h2>
            {tasks.length > 0 && (
              <span style={{ fontSize: '0.85rem', color: '#888' }}>
                {tasks.length} task{tasks.length !== 1 ? 's' : ''} total
              </span>
            )}
          </div>

          {loading ? (
            <div style={{ color: '#888', padding: '1rem' }}>Loading...</div>
          ) : tasks.length === 0 ? (
            <div style={{ color: '#888', textAlign: 'center', padding: '2rem' }}>No tasks yet</div>
          ) : (
            <>
              <div className="list">
                {paginatedTasks.map((t) => (
                  <div
                    key={t.id}
                    className="list-item"
                    onClick={() => setSelectedTask(selectedTask?.taskId === t.taskId ? null : t)}
                    style={{
                      cursor: 'pointer',
                      border: selectedTask?.taskId === t.taskId ? '2px solid #667eea' : '1px solid #eee',
                      transition: 'all 0.2s',
                    }}
                  >
                    <div className="row">
                      <div className="mono" style={{ color: '#333', fontWeight: 600 }}>
                        {t.taskId.slice(0, 8)}…
                      </div>
                      <span style={{
                        padding: '0.2rem 0.7rem', borderRadius: '999px',
                        fontSize: '0.78rem', fontWeight: 700, color: 'white',
                        background: statusColors[t.status.toLowerCase()] || '#888'
                      }}>
                        {t.status}
                      </span>
                    </div>

                    <div style={{ color: '#888', fontSize: '0.85rem', marginTop: '0.25rem' }}>
                      {t.taskType} · Priority {t.priority} · {new Date(t.createdAt).toLocaleString()}
                    </div>

                    {t.retryCount > 0 && (
                      <div style={{ color: '#f59e0b', fontSize: '0.8rem', marginTop: '0.2rem' }}>
                        ⚠️ Retried {t.retryCount}x
                      </div>
                    )}

                    {selectedTask?.taskId === t.taskId && (
                      <div style={{ marginTop: '0.75rem', paddingTop: '0.75rem', borderTop: '1px solid #eee' }}>
                        <div style={{ marginBottom: '0.5rem' }}>
                          <span style={{ fontSize: '0.75rem', fontWeight: 700, color: '#999' }}>WORKER</span>
                          <div style={{ fontSize: '0.9rem', color: '#333', marginTop: '0.1rem' }}>
                            {t.workerId
                              ? <code style={{ background: '#f3f4f6', padding: '0.1rem 0.4rem', borderRadius: '4px' }}>{t.workerId}</code>
                              : <span style={{ color: '#aaa' }}>Not assigned yet</span>
                            }
                          </div>
                        </div>

                        {t.result && (
                          <div style={{ marginBottom: '0.5rem' }}>
                            <span style={{ fontSize: '0.75rem', fontWeight: 700, color: '#999' }}>RESULT</span>
                            {(() => {
                              const parsed = parseResult(t.result)
                              return parsed ? (
                                <div style={{ marginTop: '0.25rem', background: '#f0fff4', border: '1px solid #b7ebc0', borderRadius: '6px', padding: '0.5rem' }}>
                                  {Object.entries(parsed).map(([k, v]) => (
                                    <div key={k} style={{ fontSize: '0.85rem', color: '#333' }}>
                                      <strong>{k}:</strong> {JSON.stringify(v)}
                                    </div>
                                  ))}
                                </div>
                              ) : (
                                <div style={{ fontSize: '0.85rem', color: '#4caf50', marginTop: '0.1rem' }}>✅ {t.result}</div>
                              )
                            })()}
                          </div>
                        )}

                        {t.errorMessage && (
                          <div>
                            <span style={{ fontSize: '0.75rem', fontWeight: 700, color: '#999' }}>ERROR</span>
                            <div style={{ fontSize: '0.85rem', color: '#f44336', marginTop: '0.1rem' }}>{t.errorMessage}</div>
                          </div>
                        )}

                        {!t.result && !t.errorMessage && (
                          <div style={{ fontSize: '0.85rem', color: '#888' }}>
                            {t.status === 'Processing' ? '⏳ Worker is processing this task...' : '⏳ Waiting for a worker...'}
                          </div>
                        )}
                      </div>
                    )}
                  </div>
                ))}
              </div>

              {/* Pagination */}
              {totalPages > 1 && (
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '0.5rem', marginTop: '1rem', flexWrap: 'wrap' }}>
                  <button
                    onClick={() => goToPage(1)}
                    disabled={currentPage === 1}
                    style={{ ...pageBtn, opacity: currentPage === 1 ? 0.4 : 1 }}
                  >«</button>
                  <button
                    onClick={() => goToPage(currentPage - 1)}
                    disabled={currentPage === 1}
                    style={{ ...pageBtn, opacity: currentPage === 1 ? 0.4 : 1 }}
                  >‹</button>

                  {Array.from({ length: totalPages }, (_, i) => i + 1)
                    .filter(p => p === 1 || p === totalPages || Math.abs(p - currentPage) <= 1)
                    .reduce<(number | string)[]>((acc, p, i, arr) => {
                      if (i > 0 && (p as number) - (arr[i - 1] as number) > 1) acc.push('...')
                      acc.push(p)
                      return acc
                    }, [])
                    .map((p, i) =>
                      p === '...' ? (
                        <span key={`ellipsis-${i}`} style={{ color: '#888', padding: '0 0.25rem' }}>…</span>
                      ) : (
                        <button
                          key={p}
                          onClick={() => goToPage(p as number)}
                          style={{
                            ...pageBtn,
                            background: currentPage === p ? '#667eea' : '#f3f4f6',
                            color: currentPage === p ? 'white' : '#333',
                            fontWeight: currentPage === p ? 700 : 400,
                          }}
                        >{p}</button>
                      )
                    )
                  }

                  <button
                    onClick={() => goToPage(currentPage + 1)}
                    disabled={currentPage === totalPages}
                    style={{ ...pageBtn, opacity: currentPage === totalPages ? 0.4 : 1 }}
                  >›</button>
                  <button
                    onClick={() => goToPage(totalPages)}
                    disabled={currentPage === totalPages}
                    style={{ ...pageBtn, opacity: currentPage === totalPages ? 0.4 : 1 }}
                  >»</button>

                  <span style={{ fontSize: '0.82rem', color: '#888', marginLeft: '0.5rem' }}>
                    Page {currentPage} of {totalPages}
                  </span>
                </div>
              )}
            </>
          )}
        </div>
      </div>
    </div>
  )
}

const pageBtn: React.CSSProperties = {
  padding: '0.3rem 0.65rem',
  border: '1px solid #e0e0e0',
  borderRadius: '6px',
  background: '#f3f4f6',
  color: '#333',
  cursor: 'pointer',
  fontSize: '0.85rem',
  minWidth: '32px',
}