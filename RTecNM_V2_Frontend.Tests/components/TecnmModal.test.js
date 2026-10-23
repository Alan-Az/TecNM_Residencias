import { describe, it, expect, beforeEach, afterEach } from 'vitest'
import { mount } from '@vue/test-utils'
import TecnmModal from '@/components/common/TecnmModal.vue'

describe('TecnmModal.vue', () => {
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

  it('does not render modal dialog when modelValue is false', () => {
    wrapper = mount(TecnmModal, {
      props: { modelValue: false, title: 'Test Modal' },
      attachTo: container,
    })
    expect(document.body.querySelector('.modal-backdrop')).toBeNull()
  })

  it('renders modal dialog when modelValue is true', () => {
    wrapper = mount(TecnmModal, {
      props: { modelValue: true, title: 'Prueba de Modal' },
      attachTo: container,
      slots: {
        default: '<p class="modal-test-content">Contenido del cuerpo</p>',
      },
    })

    const backdrop = document.body.querySelector('.modal-backdrop')
    expect(backdrop).not.toBeNull()
    expect(document.body.querySelector('.tecnm-modal-title').textContent).toContain('Prueba de Modal')
    expect(document.body.querySelector('.modal-test-content').textContent).toBe('Contenido del cuerpo')
  })

  it('emits update:modelValue and close when close button is clicked', async () => {
    wrapper = mount(TecnmModal, {
      props: { modelValue: true, closable: true },
      attachTo: container,
    })

    const closeBtn = document.body.querySelector('.tecnm-modal-close')
    expect(closeBtn).not.toBeNull()
    closeBtn.click()

    expect(wrapper.emitted('update:modelValue')).toBeTruthy()
    expect(wrapper.emitted('update:modelValue')[0]).toEqual([false])
    expect(wrapper.emitted('close')).toBeTruthy()
  })

  it('does not show close button when closable is false', () => {
    wrapper = mount(TecnmModal, {
      props: { modelValue: true, closable: false },
      attachTo: container,
    })

    const closeBtn = document.body.querySelector('.tecnm-modal-close')
    expect(closeBtn).toBeNull()
  })

  it('closes on Escape key press when modelValue is true and closable is true', async () => {
    wrapper = mount(TecnmModal, {
      props: { modelValue: true, closable: true },
      attachTo: container,
    })

    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }))

    expect(wrapper.emitted('update:modelValue')).toBeTruthy()
    expect(wrapper.emitted('update:modelValue')[0]).toEqual([false])
  })
})
