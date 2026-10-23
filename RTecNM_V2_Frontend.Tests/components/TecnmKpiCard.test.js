import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import TecnmKpiCard from '@/components/common/TecnmKpiCard.vue'

describe('TecnmKpiCard.vue', () => {
  it('renders title, value and subtext correctly', () => {
    const wrapper = mount(TecnmKpiCard, {
      props: {
        title: 'Estudiantes Activos',
        value: 124,
        subtext: '+12% este semestre',
      },
    })

    expect(wrapper.find('.tecnm-kpi-title').text()).toBe('Estudiantes Activos')
    expect(wrapper.find('.tecnm-kpi-value').text()).toBe('124')
    expect(wrapper.find('.tecnm-kpi-subtext').text()).toBe('+12% este semestre')
  })

  it('applies variant class according to prop', () => {
    const wrapperNavy = mount(TecnmKpiCard, {
      props: { title: 'Total', variant: 'navy' },
    })
    expect(wrapperNavy.classes()).toContain('tecnm-kpi-card--navy')

    const wrapperGold = mount(TecnmKpiCard, {
      props: { title: 'Pendientes', variant: 'gold' },
    })
    expect(wrapperGold.classes()).toContain('tecnm-kpi-card--gold')

    const wrapperEmerald = mount(TecnmKpiCard, {
      props: { title: 'Aprobados', variant: 'emerald' },
    })
    expect(wrapperEmerald.classes()).toContain('tecnm-kpi-card--emerald')
  })

  it('renders loading skeleton when loading prop is true', () => {
    const wrapper = mount(TecnmKpiCard, {
      props: {
        title: 'Cargando datos...',
        loading: true,
      },
    })

    expect(wrapper.find('.tecnm-kpi-skeleton').exists()).toBe(true)
    expect(wrapper.find('.tecnm-kpi-value').exists()).toBe(false)
  })

  it('renders custom badge and icon slots when provided', () => {
    const wrapper = mount(TecnmKpiCard, {
      props: { title: 'KPI con ranuras' },
      slots: {
        icon: '<div class="custom-icon">ICONO</div>',
        badge: '<span class="custom-badge">Nuevo</span>',
      },
    })

    expect(wrapper.find('.custom-icon').exists()).toBe(true)
    expect(wrapper.find('.custom-badge').exists()).toBe(true)
  })
})
