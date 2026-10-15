import { ref } from 'vue'

/**
 * Composable estandarizado para manejo reactivo de ordenamiento en tablas TecNM.
 * Soporta tanto ordenamiento del lado del servidor (server-side via onSortChange)
 * como ordenamiento del lado del cliente (client-side con computeds).
 *
 * @param {string} defaultSortBy - Columna inicial de ordenamiento (ej. 'ControlNumber', 'CreatedAt', 'Id')
 * @param {string} defaultSortDir - Dirección inicial ('asc' o 'desc')
 * @param {Function|null} onSortChange - Callback opcional ejecutado al cambiar orden (ej. resetear página y recargar)
 */
export function useTableSort(defaultSortBy = 'Id', defaultSortDir = 'asc', onSortChange = null) {
  const sortBy = ref(defaultSortBy)
  const sortDir = ref(defaultSortDir)

  function toggleSort(column) {
    if (!column) return

    if (sortBy.value.toLowerCase() === column.toLowerCase()) {
      sortDir.value = sortDir.value === 'asc' ? 'desc' : 'asc'
    } else {
      sortBy.value = column
      sortDir.value = 'asc'
    }

    if (typeof onSortChange === 'function') {
      onSortChange(sortBy.value, sortDir.value)
    }
  }

  function getSortIcon(column) {
    if (!column || sortBy.value.toLowerCase() !== column.toLowerCase()) return '↕'
    return sortDir.value === 'asc' ? '↑' : '↓'
  }

  function isSortActive(column) {
    if (!column) return false
    return sortBy.value.toLowerCase() === column.toLowerCase()
  }

  function setSort(column, direction = 'asc') {
    sortBy.value = column
    sortDir.value = direction
    if (typeof onSortChange === 'function') {
      onSortChange(sortBy.value, sortDir.value)
    }
  }

  return {
    sortBy,
    sortDir,
    toggleSort,
    getSortIcon,
    isSortActive,
    setSort,
  }
}
