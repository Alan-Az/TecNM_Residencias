import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import TecnmPagination from '@/components/common/TecnmPagination.vue'

describe('TecnmPagination.vue', () => {
  it('does not render anything when totalCount is 0', () => {
    const wrapper = mount(TecnmPagination, {
      props: {
        currentPage: 1,
        totalPages: 0,
        totalCount: 0,
        pageSize: 10,
      },
    })
    expect(wrapper.find('.tecnm-pagination').exists()).toBe(false)
  })

  it('renders correct records summary range', () => {
    const wrapper = mount(TecnmPagination, {
      props: {
        currentPage: 2,
        totalPages: 5,
        totalCount: 45,
        pageSize: 10,
      },
    })
    expect(wrapper.find('.tecnm-pagination-info').text()).toBe(
      'Mostrando 11–20 de 45 registro(s)'
    )
  })

  it('caps endRecord at totalCount on the last page', () => {
    const wrapper = mount(TecnmPagination, {
      props: {
        currentPage: 5,
        totalPages: 5,
        totalCount: 45,
        pageSize: 10,
      },
    })
    expect(wrapper.find('.tecnm-pagination-info').text()).toBe(
      'Mostrando 41–45 de 45 registro(s)'
    )
  })

  it('disables previous button on first page and next button on last page', () => {
    const wrapperFirst = mount(TecnmPagination, {
      props: {
        currentPage: 1,
        totalPages: 3,
        totalCount: 30,
        pageSize: 10,
      },
    })
    const prevBtnFirst = wrapperFirst.find('button[aria-label="Página anterior"]')
    const nextBtnFirst = wrapperFirst.find('button[aria-label="Página siguiente"]')
    expect(prevBtnFirst.attributes('disabled')).toBeDefined()
    expect(nextBtnFirst.attributes('disabled')).toBeUndefined()

    const wrapperLast = mount(TecnmPagination, {
      props: {
        currentPage: 3,
        totalPages: 3,
        totalCount: 30,
        pageSize: 10,
      },
    })
    const prevBtnLast = wrapperLast.find('button[aria-label="Página anterior"]')
    const nextBtnLast = wrapperLast.find('button[aria-label="Página siguiente"]')
    expect(prevBtnLast.attributes('disabled')).toBeUndefined()
    expect(nextBtnLast.attributes('disabled')).toBeDefined()
  })

  it('emits page-change and update:currentPage when clicking a page number', async () => {
    const wrapper = mount(TecnmPagination, {
      props: {
        currentPage: 1,
        totalPages: 3,
        totalCount: 30,
        pageSize: 10,
      },
    })

    const buttons = wrapper.findAll('.tecnm-pagination-btn')
    const page3Btn = buttons.find((b) => b.text().trim() === '3')
    expect(page3Btn).toBeDefined()
    await page3Btn.trigger('click')

    expect(wrapper.emitted('update:currentPage')).toBeTruthy()
    expect(wrapper.emitted('update:currentPage')[0]).toEqual([3])
    expect(wrapper.emitted('page-change')).toBeTruthy()
    expect(wrapper.emitted('page-change')[0]).toEqual([3])
  })

  it('does not emit events when clicking the currently active page', async () => {
    const wrapper = mount(TecnmPagination, {
      props: {
        currentPage: 2,
        totalPages: 3,
        totalCount: 30,
        pageSize: 10,
      },
    })

    const buttons = wrapper.findAll('.tecnm-pagination-btn')
    const activeBtn = buttons.find((b) => b.text() === '2')
    await activeBtn.trigger('click')

    expect(wrapper.emitted('page-change')).toBeFalsy()
  })

  it('renders ellipsis when totalPages is high and current page is far', () => {
    const wrapper = mount(TecnmPagination, {
      props: {
        currentPage: 5,
        totalPages: 10,
        totalCount: 100,
        pageSize: 10,
      },
    })

    const ellipses = wrapper.findAll('.tecnm-pagination-ellipsis')
    expect(ellipses.length).toBeGreaterThan(0)
  })
})
