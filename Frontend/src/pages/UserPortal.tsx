// import { useEffect, useState } from 'react'
// import { api } from '../services/api'
// import './Portal.css'

// interface Task {
//   id: number
//   taskId: string
//   status: string
//   taskType: string
//   priority: number
//   createdAt: string
//   workerId?: string
//   retryCount: number
//   result?: string
//   errorMessage?: string
// }

// export default function UserPortal() {
//   const [taskType, setTaskType] = useState('Default')
//   const [priority, setPriority] = useState(0)
//   const [payload, setPayload] = useState('{}')
//   const [submitting, setSubmitting] = useState(false)
//   const [submitMsg, setSubmitMsg] = useState<string | null>(null)

//   const [tasks, setTasks] = useState<Task[]>([])
//   const [loading, setLoading] = useState(true)

//   const loadTasks = async () => {
//     const res = await api.get('/tasks?limit=100')
//     setTasks(res.data || [])
//     setLoading(false)
//   }

//   useEffect(() => {
//     loadTasks()
//     const t = setInterval(loadTasks, 3000)
//     return () => clearInterval(t)
//   }, [])

//   const submitTask = async (e: React.FormEvent) => {
//     e.preventDefault()
//     setSubmitting(true)
//     setSubmitMsg(null)
//     try {
//       const res = await api.post('/tasks/submit', {
//         taskType,
//         priority,
//         payload,
//       })
//       setSubmitMsg(`Submitted task ${res.data.taskId}`)
//       await loadTasks()
//     } catch (err: any) {
//       setSubmitMsg(err.response?.data?.message || 'Failed to submit task')
//     } finally {
//       setSubmitting(false)
//     }
//   }

//   return (
//     <div className="portal" data-testid="user-portal">
//       <h1>User Portal</h1>

//       <div className="portal-grid">
//         <div className="card">
//           <h2>Submit Task</h2>
//           <form onSubmit={submitTask} className="form">
//             {submitMsg && <div className="note">{submitMsg}</div>}
//             <label>
//               Task Type
//               <select value={taskType} onChange={(e) => setTaskType(e.target.value)}>
//                 <option value="Compute">Compute</option>
//                 <option value="DataProcessing">DataProcessing</option>
//                 <option value="Email">Email</option>
//                 <option value="Default">Default</option>
//               </select>
//             </label>
//             <label>
//               Priority (0-10)
//               <input
//                 type="number"
//                 min={0}
//                 max={10}
//                 value={priority}
//                 onChange={(e) => setPriority(parseInt(e.target.value || '0', 10))}
//               />
//             </label>
//             <label>
//               Payload (JSON or text)
//               <textarea value={payload} onChange={(e) => setPayload(e.target.value)} rows={6} />
//             </label>
//             <button type="submit" disabled={submitting}>
//               {submitting ? 'Submitting...' : 'Submit'}
//             </button>
//           </form>
//         </div>

//         <div className="card">
//           <h2>My Recent Tasks</h2>
//           {loading ? (
//             <div className="muted">Loading...</div>
//           ) : (
//             <div className="list">
//               {tasks.length === 0 ? (
//                 <div className="muted">No tasks yet</div>
//               ) : (
//                 tasks.map((t) => (
//                   <div key={t.id} className="list-item">
//                     <div className="row">
//                       <div className="mono">{t.taskId.slice(0, 8)}…</div>
//                       <div className={`badge ${t.status.toLowerCase()}`}>{t.status}</div>
//                     </div>
//                     <div className="muted">
//                       {t.taskType} • prio {t.priority} • {new Date(t.createdAt).toLocaleString()}
//                     </div>
//                     {t.errorMessage && <div className="error">{t.errorMessage}</div>}
//                   </div>
//                 ))
//               )}
//             </div>
//           )}
//         </div>
//       </div>
//     </div>
//   )
// }


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
}

// Payload mẫu cho từng TaskType
const payloadSamples: Record<string, string> = {
  Compute: JSON.stringify({ operation: 'add', a: 5, b: 3 }, null, 2),
  DataProcessing: JSON.stringify({ inputData: [1, 2, 3], action: 'sum' }, null, 2),
  Email: JSON.stringify({ to: 'user@example.com', subject: 'Hello', body: 'Message here' }, null, 2),
  Default: JSON.stringify({ data: 'your data here' }, null, 2),
}

export default function UserPortal() {
  const [taskType, setTaskType] = useState('Default')
  const [priority, setPriority] = useState(0)
  const [payload, setPayload] = useState(payloadSamples['Default'])
  const [submitting, setSubmitting] = useState(false)
  const [submitMsg, setSubmitMsg] = useState<string | null>(null)

  const [tasks, setTasks] = useState<Task[]>([])
  const [loading, setLoading] = useState(true)

  // Khi đổi TaskType → tự điền payload mẫu
  const handleTaskTypeChange = (newType: string) => {
    setTaskType(newType)
    setPayload(payloadSamples[newType] || '{}')
  }

  const loadTasks = async () => {
    const res = await api.get('/tasks?limit=100')
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

    // Validate JSON trước khi gửi
    try {
      JSON.parse(payload)
    } catch {
      setSubmitMsg('Payload phải là JSON hợp lệ!')
      setSubmitting(false)
      return
    }

    try {
      const res = await api.post('/tasks/submit', {
        taskType,
        priority,
        payload,
      })
      setSubmitMsg(`Submitted task ${res.data.taskId}`)
      await loadTasks()
    } catch (err: any) {
      setSubmitMsg(err.response?.data?.message || 'Failed to submit task')
    } finally {
      setSubmitting(false)
    }
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
                type="number"
                min={0}
                max={10}
                value={priority}
                onChange={(e) => setPriority(parseInt(e.target.value || '0', 10))}
              />
            </label>
            <label>
              Payload (JSON)
              <textarea value={payload} onChange={(e) => setPayload(e.target.value)} rows={6} />
            </label>
            <button type="submit" disabled={submitting}>
              {submitting ? 'Submitting...' : 'Submit'}
            </button>
          </form>
        </div>

        <div className="card">
          <h2>My Recent Tasks</h2>
          {loading ? (
            <div className="muted">Loading...</div>
          ) : (
            <div className="list">
              {tasks.length === 0 ? (
                <div className="muted">No tasks yet</div>
              ) : (
                tasks.map((t) => (
                  <div key={t.id} className="list-item">
                    <div className="row">
                      <div className="mono">{t.taskId.slice(0, 8)}…</div>
                      <div className={`badge ${t.status.toLowerCase()}`}>{t.status}</div>
                    </div>
                    <div className="muted">
                      {t.taskType} • prio {t.priority} • {new Date(t.createdAt).toLocaleString()}
                    </div>
                    {t.errorMessage && <div className="error">{t.errorMessage}</div>}
                  </div>
                ))
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  )
}