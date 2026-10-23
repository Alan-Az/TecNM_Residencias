import { describe, it, expect, beforeEach, afterEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import ConfirmModal from '@/components/common/ConfirmModal.vue'
import { useConfirm } from '@/composables/useConfirm'

describe('ConfirmModal.vue', () => {
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

  it('renders modal when useConfirm triggers dialog', async () => {
    wrapper = mount(ConfirmModal, { attachTo: container })

    const { confirm, isVisible } = useConfirm()
    confirm({
      title: 'Confirmar Cancelación',
      message: '¿Está seguro de cancelar el anteproyecto?',
      okText: 'Aceptar',
      cancelText: 'Volver',
    })
    await nextTick()

    expect(isVisible.value).toBe(true)
    const modalTitle = document.body.querySelector('.tecnm-modal-title')
    expect(modalTitle).not.toBeNull()
    expect(modalTitle.textContent).toBe('Confirmar Cancelación')

    const bodyText = document.body.querySelector('.tecnm-card-body p')
    expect(bodyText.textContent).toContain('¿Está seguro de cancelar el anteproyecto?')
  })

  it('closes dialog when cancel button is clicked', async () => {
    wrapper = mount(ConfirmModal, { attachTo: container })

    const { confirm, isVisible } = useConfirm()
    const promise = confirm({ message: 'Pregunta' })
    await nextTick()

    const cancelBtn = document.body.querySelector('.tecnm-btn-secondary')
    expect(cancelBtn).not.toBeNull()
    cancelBtn.click()
    await nextTick()

    const result = await promise
    expect(result).toBe(false)
    expect(isVisible.value).toBe(false)
  })
})
