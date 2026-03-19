import { useState } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'
import './Auth.css'

export default function Register() {
  const [username, setUsername] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [role, setRole] = useState<'User' | 'Worker'>('User')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)
  const { register } = useAuth()
  const navigate = useNavigate()

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError('')
    setLoading(true)
    try {
      await register(username, email, password, role)
      navigate(role === 'Worker' ? '/worker' : '/portal')
    } catch (err: any) {
      setError(err.response?.data?.message || 'Registration failed. Please try again.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="auth-container">
      <div className="auth-card">
        <h2>Create Account</h2>
        <form onSubmit={handleSubmit}>
          {error && <div className="error-message">{error}</div>}

          <div className="form-group">
            <label htmlFor="username">Username</label>
            <input
              id="username"
              type="text"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              required
              data-testid="register-username"
            />
          </div>

          <div className="form-group">
            <label htmlFor="email">Email</label>
            <input
              id="email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              data-testid="register-email"
            />
          </div>

          <div className="form-group">
            <label htmlFor="password">Password</label>
            <input
              id="password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              data-testid="register-password"
            />
          </div>

          <div className="form-group">
            <label htmlFor="role">Register As</label>
            <select
              id="role"
              value={role}
              onChange={(e) => setRole(e.target.value as 'User' | 'Worker')}
              required
              data-testid="register-role"
            >
              <option value="User">👤 User — submit tasks</option>
              <option value="Worker">⚙️ Worker — process tasks</option>
            </select>
          </div>

          <div className={`role-info ${role === 'Worker' ? 'role-info--worker' : 'role-info--user'}`}>
            {role === 'Worker' ? (
              <>
                <span className="role-info__icon">⚙️</span>
                <div>
                  <strong>Worker Account</strong>
                  <p>Browse and claim tasks from other users. Your worker node will be registered automatically.</p>
                </div>
              </>
            ) : (
              <>
                <span className="role-info__icon">👤</span>
                <div>
                  <strong>User Account</strong>
                  <p>Submit tasks to the distributed queue and track their progress in real time.</p>
                </div>
              </>
            )}
          </div>

          <button type="submit" disabled={loading} data-testid="register-submit">
            {loading ? 'Registering...' : `Register as ${role}`}
          </button>
        </form>

        <p className="auth-link">
          Already have an account? <Link to="/login">Login here</Link>
        </p>
      </div>
    </div>
  )
}