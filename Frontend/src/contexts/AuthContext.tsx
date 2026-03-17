import React, { createContext, useContext, useState, useEffect } from 'react'
import { api } from '../services/api'

interface User {
  username: string
  role: string
}

interface AuthContextType {
  user: User | null
  token: string | null
  isAuthenticated: boolean
  login: (username: string, password: string) => Promise<void>
  register: (username: string, email: string, password: string, role: string) => Promise<void>
  logout: () => void
}

const AuthContext = createContext<AuthContextType | undefined>(undefined)

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [token, setToken] = useState<string | null>(null)

  useEffect(() => {
    const storedToken = localStorage.getItem('token')
    const storedUser = localStorage.getItem('user')
    if (storedToken && storedUser) {
      setToken(storedToken)
      setUser(JSON.parse(storedUser))
      api.defaults.headers.common['Authorization'] = `Bearer ${storedToken}`
    }
  }, [])

  const login = async (username: string, password: string) => {
    const response = await api.post('/auth/login', { username, password })
    const { token: newToken, username: userUsername, role } = response.data
    setToken(newToken)
    setUser({ username: userUsername, role })
    localStorage.setItem('token', newToken)
    localStorage.setItem('user', JSON.stringify({ username: userUsername, role }))
    api.defaults.headers.common['Authorization'] = `Bearer ${newToken}`
  }

  const register = async (username: string, email: string, password: string, role: string) => {
    const response = await api.post('/auth/register', { username, email, password, role })
    const { token: newToken, username: userUsername, role: returnedRole } = response.data
    setToken(newToken)
    setUser({ username: userUsername, role: returnedRole })
    localStorage.setItem('token', newToken)
    localStorage.setItem('user', JSON.stringify({ username: userUsername, role: returnedRole }))
    api.defaults.headers.common['Authorization'] = `Bearer ${newToken}`
  }

  const logout = () => {
    setToken(null)
    setUser(null)
    localStorage.removeItem('token')
    localStorage.removeItem('user')
    delete api.defaults.headers.common['Authorization']
  }

  return (
    <AuthContext.Provider
      value={{
        user,
        token,
        isAuthenticated: !!token,
        login,
        register,
        logout,
      }}
    >
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return context
}



