import { describe, it, expect } from 'vitest'
import { useAudit } from '@/composables/useAudit'

describe('useAudit.js', () => {
  it('formats audit dates correctly in Spanish', () => {
    const { formatAuditDate } = useAudit()

    expect(formatAuditDate(null)).toBe('—')
    expect(formatAuditDate('invalid-date')).toBe('—')

    const formatted = formatAuditDate('2026-05-15T10:00:00Z')
    expect(formatted).toMatch(/15\/Mayo\/2026/)
  })

  it('formats system creator as "Sistema (semilla)" when id is null/0', () => {
    const { formatAuditUser } = useAudit()
    expect(formatAuditUser(null)).toBe('Sistema (semilla)')
    expect(formatAuditUser(0)).toBe('Sistema (semilla)')
  })

  it('populates 10-field audit structure on showAudit', async () => {
    const { isVisible, auditRows, auditTitle, showAudit, close } = useAudit()

    await showAudit({
      title: 'Auditoría de Formato Oficial',
      item: {
        id: 42,
        title: 'Formato Solicitud',
        isActive: true,
        isVisible: true,
        displayOrder: 1,
        createdAt: '2026-01-10T08:00:00Z',
        createdBy: null,
      },
    })

    expect(isVisible.value).toBe(true)
    expect(auditTitle.value).toBe('Auditoría de Formato Oficial')
    expect(auditRows.value.length).toBeGreaterThanOrEqual(8)

    const idRow = auditRows.value.find((r) => r.label === 'ID')
    expect(idRow.value).toBe(42)

    const stateRow = auditRows.value.find((r) => r.label === 'Estado')
    expect(stateRow.value).toBe('Activo')

    close()
    expect(isVisible.value).toBe(false)
  })
})
