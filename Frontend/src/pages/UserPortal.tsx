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
  const [advancedMode, setAdvancedMode] = useState(false)
  const [formData, setFormData] = useState({
    operation: 'add', a: '5', b: '3', inputData: '1, 2, 3',
    action: 'sum', to: '', subject: '', body: '', data: ''
  })
  const updateField = (field: keyof typeof formData, value: string) =>
    setFormData(prev => ({ ...prev, [field]: value }))

  const buildPayload = (): string => {
    if (advancedMode) {
      const parsed: unknown = JSON.parse(payload)
      if (parsed === null || typeof parsed !== 'object' || Array.isArray(parsed)) {
        throw new Error('Payload must be a JSON object.')
      }
      return payload
    }
    switch (taskType) {
      case 'Compute': {
        if (!formData.a.trim() || !formData.b.trim())
          throw new Error('Enter both numbers.')
        const a = Number(formData.a), b = Number(formData.b)
        if (!Number.isFinite(a) || !Number.isFinite(b))
          throw new Error('Enter valid numbers.')
        if (formData.operation === 'divide' && b === 0)
          throw new Error('Cannot divide by zero.')
        return JSON.stringify({ operation: formData.operation, a, b })
      }
      case 'DataProcessing': {
        const parts = formData.inputData.split(',').map(x => x.trim())
        if (parts.some(x => !x || !Number.isFinite(Number(x))))
          throw new Error('Enter at least one valid number, separated by commas.')
        return JSON.stringify({ inputData: parts.map(Number), action: formData.action })
      }
      case 'Email': {
        if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(formData.to.trim()))
          throw new Error('Enter a valid recipient email.')
        if (!formData.subject.trim() || !formData.body.trim())
          throw new Error('Subject and message are required.')
        return JSON.stringify({ to: formData.to.trim(), subject: formData.subject, body: formData.body })
      }
      case 'Default':
        if (!formData.data.trim()) throw new Error('Enter task data.')
        return JSON.stringify({ data: formData.data })
      default:
        throw new Error('Unsupported task type.')
    }
  }
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
    setSubmitMsg(null)
    if (!Number.isInteger(priority) || priority < 0 || priority > 10) {
      setSubmitMsg('Priority must be between 0 and 10.')
      return
    }
    let generatedPayload: string
    try {
      generatedPayload = buildPayload()
    } catch (err) {
      setSubmitMsg(err instanceof Error ? err.message : 'Invalid task data.')
      return
    }
    setSubmitting(true)
    try {
      const res = await api.post('/tasks/submit', {
        taskType, priority, payload: generatedPayload
      })
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
          <h2>Submit Task  tetstsdst</h2>
          <form onSubmit={submitTask} className="form">
            {submitMsg && <div className="note" role="status">{submitMsg}</div>}
            <label>
              Task Type
              <select value={taskType} onChange={e => handleTaskTypeChange(e.target.value)}>
                <option value="Compute">Compute</option>
                <option value="DataProcessing">Data Processing</option>
                <option value="Email">Email (Simulation)</option>
                <option value="Default">Default</option>
              </select>
            </label>

            {!advancedMode && taskType === 'Compute' && <>
              <label>Operation
                <select value={formData.operation} onChange={e => updateField('operation', e.target.value)}>
                  <option value="add">Addition (+)</option>
                  <option value="subtract">Subtraction (-)</option>
                  <option value="multiply">Multiplication (×)</option>
                  <option value="divide">Division (÷)</option>
                </select>
              </label>
              <label>Number A
                <input type="number" step="any" required value={formData.a}
                  onChange={e => updateField('a', e.target.value)} />
              </label>
              <label>Number B
                <input type="number" step="any" required value={formData.b}
                  onChange={e => updateField('b', e.target.value)} />
              </label>
            </>}

            {!advancedMode && taskType === 'DataProcessing' && <>
              <label>Numbers (comma separated)
                <input required placeholder="1, 2, 3" value={formData.inputData}
                  onChange={e => updateField('inputData', e.target.value)} />
              </label>
              <label>Action
                <select value={formData.action} onChange={e => updateField('action', e.target.value)}>
                  <option value="sum">Sum</option>
                  <option value="average">Average</option>
                  <option value="max">Maximum</option>
                  <option value="min">Minimum</option>
                </select>
              </label>
            </>}

            {!advancedMode && taskType === 'Email' && <>
              <p style={{ color: '#805600', fontSize: '0.9rem' }}>
                This task simulates sending an email; no real email is sent.
              </p>
              <label>Recipient Email
                <input type="email" required placeholder="user@example.com" value={formData.to}
                  onChange={e => updateField('to', e.target.value)} />
              </label>
              <label>Subject
                <input required value={formData.subject}
                  onChange={e => updateField('subject', e.target.value)} />
              </label>
              <label>Message
                <textarea required rows={4} value={formData.body}
                  onChange={e => updateField('body', e.target.value)} />
              </label>
            </>}

            {!advancedMode && taskType === 'Default' &&
              <label>Task Data
                <textarea required rows={4} placeholder="Enter your task data"
                  value={formData.data} onChange={e => updateField('data', e.target.value)} />
              </label>
            }

            <label>Priority: {priority} / 10
              <input type="range" min={0} max={10} step={1} value={priority}
                onChange={e => setPriority(Number(e.target.value))} />
            </label>
            <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
              <input type="checkbox" checked={advancedMode}
                onChange={e => setAdvancedMode(e.target.checked)}
                style={{ width: 'auto' }} />
              Advanced Mode (Raw JSON)
            </label>
            {advancedMode && <label>Payload (JSON)
              <textarea value={payload} onChange={e => setPayload(e.target.value)} rows={6} />
            </label>}
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