import { describe, it, expect, vi } from 'vitest'
import { useTableSort } from '@/composables/useTableSort'

describe('useTableSort.js', () => {
  it('initializes with default values', () => {
    const { sortBy, sortDir, isSortActive, getSortIcon } = useTableSort('CreatedAt', 'desc')

    expect(sortBy.value).toBe('CreatedAt')
    expect(sortDir.value).toBe('desc')
    expect(isSortActive('CreatedAt')).toBe(true)
    expect(isSortActive('Name')).toBe(false)
    expect(getSortIcon('CreatedAt')).toBe('↓')
    expect(getSortIcon('Name')).toBe('↕')
  })

  it('toggles direction when same column is selected', () => {
    const { sortBy, sortDir, toggleSort, getSortIcon } = useTableSort('Title', 'asc')

    toggleSort('Title')
    expect(sortBy.value).toBe('Title')
    expect(sortDir.value).toBe('desc')
    expect(getSortIcon('Title')).toBe('↓')

    toggleSort('Title')
    expect(sortDir.value).toBe('asc')
    expect(getSortIcon('Title')).toBe('↑')
  })

  it('resets direction to asc when switching to a different column', () => {
    const { sortBy, sortDir, toggleSort } = useTableSort('Title', 'desc')

    toggleSort('ControlNumber')
    expect(sortBy.value).toBe('ControlNumber')
    expect(sortDir.value).toBe('asc')
  })

  it('invokes onSortChange callback on sort change', () => {
    const callback = vi.fn()
    const { toggleSort, setSort } = useTableSort('Id', 'asc', callback)

    toggleSort('Id')
    expect(callback).toHaveBeenCalledWith('Id', 'desc')

    setSort('StudentName', 'desc')
    expect(callback).toHaveBeenCalledWith('StudentName', 'desc')
  })

  it('handles case-insensitivity on column matching', () => {
    const { isSortActive, toggleSort, sortDir } = useTableSort('createdAt', 'asc')

    expect(isSortActive('CREATEDAT')).toBe(true)
    toggleSort('CREATEDAT')
    expect(sortDir.value).toBe('desc')
  })
})
