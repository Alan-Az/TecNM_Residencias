<script setup>
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { useAuthStore } from '@/stores/auth'

const props = defineProps({
  loading: {
    type: Boolean,
    default: false,
  },
  disabled: {
    type: Boolean,
    default: false,
  },
  formats: {
    type: [Array, String],
    default: () => ['pdf'],
  },
  label: {
    type: String,
    default: '',
  },
  id: {
    type: String,
    default: '',
  },
})

const emit = defineEmits(['export'])
const authStore = useAuthStore()

const normalizedFormats = computed(() => {
  if (Array.isArray(props.formats)) return props.formats.map((f) => String(f).toLowerCase())
  if (typeof props.formats === 'string') return [props.formats.toLowerCase()]
  return ['pdf']
})

const isSingleFormat = computed(() => normalizedFormats.value.length === 1)
const singleFormat = computed(() => normalizedFormats.value[0] || 'pdf')

const displayLabel = computed(() => {
  if (props.loading) {
    return singleFormat.value === 'excel' ? 'Descargando...' : 'Exportando...'
  }
  if (props.label) return props.label
  if (isSingleFormat.value) {
    return singleFormat.value === 'excel' ? 'Descargar Excel' : 'Exportar PDF'
  }
  return 'Exportar'
})

const isDropdownOpen = ref(false)
const dropdownRef = ref(null)

function toggleDropdown() {
  if (props.disabled || props.loading) return
  isDropdownOpen.value = !isDropdownOpen.value
}

function handleExport(format) {
  isDropdownOpen.value = false
  emit('export', format)
}

function handleClickOutside(event) {
  if (dropdownRef.value && !dropdownRef.value.contains(event.target)) {
    isDropdownOpen.value = false
  }
}

onMounted(() => {
  document.addEventListener('click', handleClickOutside)
})

onBeforeUnmount(() => {
  document.removeEventListener('click', handleClickOutside)
})
</script>

<template>
  <!-- Blindaje de Seguridad RBAC: Estudiantes jamás pueden visualizar este botón -->
  <template v-if="!authStore.isStudent">
    <!-- Caso 1: Formato Único (Botón Directo) -->
    <button
      v-if="isSingleFormat"
      :id="id || undefined"
      type="button"
      class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm tecnm-export-btn"
      :disabled="disabled || loading"
      @click="handleExport(singleFormat)"
    >
      <svg
        v-if="!loading"
        xmlns="http://www.w3.org/2000/svg"
        width="15"
        height="15"
        fill="none"
        viewBox="0 0 24 24"
        stroke="currentColor"
        stroke-width="2"
        aria-hidden="true"
        class="tecnm-export-icon"
      >
        <path
          stroke-linecap="round"
          stroke-linejoin="round"
          d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5m-13.5-9L12 3m0 0l4.5 4.5M12 3v13.5"
        />
      </svg>
      <span v-else class="tecnm-export-spinner" aria-hidden="true"></span>
      <span>{{ displayLabel }}</span>
    </button>

    <!-- Caso 2: Múltiples Formatos (Dropdown) -->
    <div v-else ref="dropdownRef" class="tecnm-export-dropdown-wrapper">
      <button
        :id="id || undefined"
        type="button"
        class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm tecnm-export-btn"
        :disabled="disabled || loading"
        aria-haspopup="true"
        :aria-expanded="isDropdownOpen"
        @click.stop="toggleDropdown"
      >
        <svg
          v-if="!loading"
          xmlns="http://www.w3.org/2000/svg"
          width="15"
          height="15"
          fill="none"
          viewBox="0 0 24 24"
          stroke="currentColor"
          stroke-width="2"
          aria-hidden="true"
          class="tecnm-export-icon"
        >
          <path
            stroke-linecap="round"
            stroke-linejoin="round"
            d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5m-13.5-9L12 3m0 0l4.5 4.5M12 3v13.5"
          />
        </svg>
        <span v-else class="tecnm-export-spinner" aria-hidden="true"></span>
        <span>{{ displayLabel }}</span>
        <span class="tecnm-export-caret" aria-hidden="true">▾</span>
      </button>

      <div v-if="isDropdownOpen" class="tecnm-export-menu" role="menu">
        <button
          v-if="normalizedFormats.includes('pdf')"
          type="button"
          class="tecnm-export-menu-item"
          role="menuitem"
          @click="handleExport('pdf')"
        >
          <span class="tecnm-export-menu-badge pdf">PDF</span>
          <span>Documento PDF Oficial</span>
        </button>
        <button
          v-if="normalizedFormats.includes('excel')"
          type="button"
          class="tecnm-export-menu-item"
          role="menuitem"
          @click="handleExport('excel')"
        >
          <span class="tecnm-export-menu-badge excel">XLS</span>
          <span>Hoja de Cálculo Excel</span>
        </button>
      </div>
    </div>
  </template>
</template>

<style scoped>
.tecnm-export-btn {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  font-weight: 500;
  white-space: nowrap;
}

.tecnm-export-icon {
  flex-shrink: 0;
}

.tecnm-export-caret {
  font-size: 0.75rem;
  margin-left: 0.2rem;
}

.tecnm-export-spinner {
  display: inline-block;
  width: 13px;
  height: 13px;
  border: 2px solid rgba(27, 57, 106, 0.25);
  border-top-color: var(--tecnm-blue, #1B396A);
  border-radius: 50%;
  animation: tecnm-spin 0.6s linear infinite;
  margin-right: 0.2rem;
}

@keyframes tecnm-spin {
  to {
    transform: rotate(360deg);
  }
}

.tecnm-export-dropdown-wrapper {
  position: relative;
  display: inline-block;
}

.tecnm-export-menu {
  position: absolute;
  top: 100%;
  right: 0;
  margin-top: 0.25rem;
  background-color: var(--tecnm-surface-white, #ffffff);
  border: 1px solid var(--tecnm-border-color, #cbd5e1);
  border-radius: var(--tecnm-radius-sm, 6px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
  min-width: 190px;
  z-index: 50;
  overflow: hidden;
}

.tecnm-export-menu-item {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  width: 100%;
  padding: 0.5rem 0.75rem;
  font-size: 0.85rem;
  text-align: left;
  border: none;
  background: transparent;
  color: var(--tecnm-text-primary, #0f172a);
  cursor: pointer;
  transition: background-color 0.15s ease;
}

.tecnm-export-menu-item:hover {
  background-color: var(--tecnm-bg-hover, #f1f5f9);
}

.tecnm-export-menu-badge {
  font-size: 0.7rem;
  font-weight: 700;
  padding: 0.15rem 0.35rem;
  border-radius: 4px;
}

.tecnm-export-menu-badge.pdf {
  background-color: #fee2e2;
  color: #b91c1c;
}

.tecnm-export-menu-badge.excel {
  background-color: #dcfce7;
  color: #15803d;
}
</style>
