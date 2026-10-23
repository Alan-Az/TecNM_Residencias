import { describe, it, expect, beforeEach, afterEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import AuditModal from '@/components/common/AuditModal.vue'
import { useAudit } from '@/composables/useAudit'

describe('AuditModal.vue', () => {
  let wrapper
  let container

  beforeEach(() => {
    container = document.createElement('div')
    document.body.appendChild(container)
  })

  afterEach(() => {
    if (wrapper) {
      wrapper.unmount()
      wrapper = null
    }
    if (container) {
      container.remove()
      container = null
    }
  })

  it('renders audit entries when showAudit is invoked', async () => {
    wrapper = mount(AuditModal, { attachTo: container })

    const { showAudit, isVisible } = useAudit()
    await showAudit({
      title: 'Auditoría Institucional',
      item: { id: 10, name: 'Convenio Marco', isActive: true, createdAt: '2026-02-01T00:00:00Z' },
    })
    await nextTick()

    expect(isVisible.value).toBe(true)
    const titleEl = document.body.querySelector('#auditModalTitle')
    expect(titleEl).not.toBeNull()
    expect(titleEl.textContent).toBe('Auditoría Institucional')

    const rows = document.body.querySelectorAll('.tecnm-audit-row')
    expect(rows.length).toBeGreaterThan(0)
  })

  it('closes modal when ok button is clicked', async () => {
    wrapper = mount(AuditModal, { attachTo: container })

    const { showAudit, isVisible } = useAudit()
    await showAudit({
      title: 'Auditoría',
      item: { id: 1 },
    })
    await nextTick()

    const okBtn = document.body.querySelector('#auditModalOk')
    expect(okBtn).not.toBeNull()
    okBtn.click()
    await nextTick()

    expect(isVisible.value).toBe(false)
  })
})
