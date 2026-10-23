import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import TecnmBadge from '@/components/common/TecnmBadge.vue'

describe('TecnmBadge.vue', () => {
  it('renders default slot text when provided', () => {
    const wrapper = mount(TecnmBadge, {
      slots: {
        default: 'Texto Personalizado',
      },
    })
    expect(wrapper.text()).toContain('Texto Personalizado')
  })

  it('renders "Activo" and approved class for status=true or status=1', () => {
    const wrapperBool = mount(TecnmBadge, {
      props: { status: true },
    })
    expect(wrapperBool.text()).toBe('Activo')
    expect(wrapperBool.classes()).toContain('tecnm-badge-approved')

    const wrapperNum = mount(TecnmBadge, {
      props: { status: 1 },
    })
    expect(wrapperNum.text()).toBe('Activo')
    expect(wrapperNum.classes()).toContain('tecnm-badge-approved')
  })

  it('renders "Inactivo" and rejected class for status=false or status=0', () => {
    const wrapper = mount(TecnmBadge, {
      props: { status: false },
    })
    expect(wrapper.text()).toBe('Inactivo')
    expect(wrapper.classes()).toContain('tecnm-badge-rejected')
  })

  it('maps "draft" and "borrador" status to neutral class', () => {
    const wrapperDraft = mount(TecnmBadge, {
      props: { status: 'draft' },
    })
    expect(wrapperDraft.text()).toBe('Borrador')
    expect(wrapperDraft.classes()).toContain('tecnm-badge-neutral')

    const wrapperBorrador = mount(TecnmBadge, {
      props: { status: 'borrador' },
    })
    expect(wrapperBorrador.text()).toBe('Borrador')
    expect(wrapperBorrador.classes()).toContain('tecnm-badge-neutral')
  })

  it('maps "approved" status to approved class', () => {
    const wrapper = mount(TecnmBadge, {
      props: { status: 'approved' },
    })
    expect(wrapper.text()).toBe('Aprobado')
    expect(wrapper.classes()).toContain('tecnm-badge-approved')
  })

  it('maps "in_progress" status to approved class and label', () => {
    const wrapper = mount(TecnmBadge, {
      props: { status: 'in_progress' },
    })
    expect(wrapper.text()).toBe('En Progreso')
    expect(wrapper.classes()).toContain('tecnm-badge-approved')
  })

  it('maps "rejected" status to "Correcciones Requeridas" and rejected class', () => {
    const wrapper = mount(TecnmBadge, {
      props: { status: 'rejected' },
    })
    expect(wrapper.text()).toBe('Correcciones Requeridas')
    expect(wrapper.classes()).toContain('tecnm-badge-rejected')
  })

  it('maps "cancelled" status to "Cancelado" and rejected class', () => {
    const wrapper = mount(TecnmBadge, {
      props: { status: 'cancelled' },
    })
    expect(wrapper.text()).toBe('Cancelado')
    expect(wrapper.classes()).toContain('tecnm-badge-rejected')
  })

  it('falls back to "En Revisión" and pending class for unknown status', () => {
    const wrapper = mount(TecnmBadge, {
      props: { status: 'desconocido' },
    })
    expect(wrapper.text()).toBe('En Revisión')
    expect(wrapper.classes()).toContain('tecnm-badge-pending')
  })

  it('uses custom label and custom type when specified', () => {
    const wrapper = mount(TecnmBadge, {
      props: {
        status: 'approved',
        label: 'Aprobado con Distinción',
        type: 'warning',
      },
    })
    expect(wrapper.text()).toBe('Aprobado con Distinción')
    expect(wrapper.classes()).toContain('tecnm-badge-warning')
  })
})
