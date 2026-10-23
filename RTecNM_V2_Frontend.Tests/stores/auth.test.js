import { describe, it, expect, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useAuthStore } from '@/stores/auth'

describe('auth.js Store', () => {
  beforeEach(() => {
    sessionStorage.clear()
    setActivePinia(createPinia())
  })

  it('initializes with unauthenticated empty state when sessionStorage is empty', () => {
    const auth = useAuthStore()

    expect(auth.isAuthenticated).toBe(false)
    expect(auth.user).toBeNull()
    expect(auth.currentRole).toBe('')
    expect(auth.isAdmin).toBe(false)
  })

  it('computes roles and isAdmin correctly for admin user', () => {
    const auth = useAuthStore()
    auth.token = 'fake-jwt-token'
    auth.user = {
      id: 1,
      fullName: 'Administrador General',
      role: 'admin',
      isAdmin: true,
      email: 'admin@monclova.tecnm.mx',
    }

    expect(auth.isAuthenticated).toBe(true)
    expect(auth.currentRole).toBe('admin')
    expect(auth.isAdmin).toBe(true)
    expect(auth.isStaff).toBe(true)
    expect(auth.isStudent).toBe(false)
    expect(auth.roleLabel).toBe('Administrador')
    expect(auth.userDisplayName).toBe('Administrador General')
    expect(auth.userAvatarInitials).toBe('AG')
  })

  it('computes student role and helpers correctly', () => {
    const auth = useAuthStore()
    auth.token = 'fake-jwt-token'
    auth.user = {
      id: 20,
      fullName: 'Juan Perez',
      role: 'student',
      controlNumber: '20680123',
      email: 'juan.perez@monclova.tecnm.mx',
    }

    expect(auth.isAuthenticated).toBe(true)
    expect(auth.currentRole).toBe('student')
    expect(auth.isAdmin).toBe(false)
    expect(auth.isStudent).toBe(true)
    expect(auth.isStaff).toBe(false)
    expect(auth.hasRole('student')).toBe(true)
    expect(auth.hasRole('admin')).toBe(false)
    expect(auth.roleLabel).toBe('Estudiante')
    expect(auth.userAvatarInitials).toBe('JP')
  })

  it('computes advisor role and advisor ID correctly', () => {
    const auth = useAuthStore()
    auth.token = 'fake-jwt-token'
    auth.user = {
      id: 15,
      fullName: 'Dr. Fernando Rivera',
      role: 'advisor',
      advisorId: 99,
      email: 'fernando.rivera@monclova.tecnm.mx',
    }

    expect(auth.isAuthenticated).toBe(true)
    expect(auth.currentRole).toBe('advisor')
    expect(auth.isAdvisor).toBe(true)
    expect(auth.userAdvisorId).toBe(99)
    expect(auth.roleLabel).toBe('Asesor Académico')
  })

  it('computes readOnly correctly for director and coordinator', () => {
    const auth = useAuthStore()
    auth.token = 'fake-jwt-token'
    auth.user = {
      id: 5,
      role: 'director',
    }

    expect(auth.isReadOnly).toBe(true)

    auth.user = {
      id: 6,
      role: 'coordinadora',
    }
    expect(auth.isReadOnly).toBe(true)
    expect(auth.isCoordinator).toBe(true)
  })

  it('verifies default role permissions for vinculacion', () => {
    const auth = useAuthStore()
    auth.token = 'fake-jwt-token'
    auth.user = {
      id: 7,
      role: 'vinculacion',
      permissions: [],
    }

    expect(auth.hasPermission('companies.view')).toBe(true)
    expect(auth.hasPermission('admin.reports')).toBe(false)
  })
})
