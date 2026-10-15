<script setup>
import { ref, watch, onBeforeUnmount } from 'vue'

const props = defineProps({
  modelValue: {
    type: String,
    default: '',
  },
  searchPlaceholder: {
    type: String,
    default: 'Buscar por nombre, palabra clave o folio...',
  },
  showSearch: {
    type: Boolean,
    default: true,
  },
  debounceMs: {
    type: Number,
    default: 350,
  },
  searchId: {
    type: String,
    default: '',
  },
})

const emit = defineEmits(['update:modelValue', 'search', 'clear'])

const internalSearch = ref(props.modelValue || '')
let debounceTimer = null

// Sincronizar hacia adentro cuando el padre muta el valor externamente (ej. GlobalSearchModal)
watch(
  () => props.modelValue,
  (newVal) => {
    if (newVal !== internalSearch.value) {
      internalSearch.value = newVal || ''
    }
  }
)

function onInput(event) {
  const value = event.target.value
  internalSearch.value = value
  emit('update:modelValue', value)

  clearTimeout(debounceTimer)
  debounceTimer = setTimeout(() => {
    emit('search', value.trim())
  }, props.debounceMs)
}

function onKeydownEnter() {
  clearTimeout(debounceTimer)
  emit('search', internalSearch.value.trim())
}

function onClear() {
  clearTimeout(debounceTimer)
  internalSearch.value = ''
  emit('update:modelValue', '')
  emit('clear')
  emit('search', '')
}

onBeforeUnmount(() => {
  clearTimeout(debounceTimer)
})
</script>

<template>
  <div class="tecnm-card-toolbar">
    <!-- Buscador Integrado Estandarizado -->
    <div
      v-if="showSearch"
      class="tecnm-form-group tecnm-mb-0 tecnm-search-box tecnm-search-standard-container"
    >
      <input
        :id="searchId || undefined"
        :value="internalSearch"
        type="search"
        class="tecnm-form-control tecnm-search-standard-input"
        :placeholder="searchPlaceholder"
        autocomplete="off"
        @input="onInput"
        @keydown.enter.prevent="onKeydownEnter"
      />
    </div>

    <!-- Slot para Filtros (Carrera, Asesor, Estatus, etc.) -->
    <div v-if="$slots.filters" class="tecnm-toolbar-filters">
      <slot name="filters" />
    </div>

    <!-- Slot para Acciones y Herramientas (Toggles, Exportación, Botón Nuevo) -->
    <div v-if="$slots.actions" class="tecnm-toolbar-actions">
      <slot name="actions" />
    </div>
  </div>
</template>

<style scoped>
.tecnm-search-standard-container {
  margin-bottom: 0;
  flex: 1 1 240px;
  max-width: 360px;
  min-width: 200px;
}

.tecnm-search-standard-input {
  height: 2.375rem;
  font-size: 0.875rem;
  padding: 0.45rem 0.75rem;
  border-radius: var(--tecnm-radius-sm, 6px);
  border: 1px solid var(--tecnm-border-color, #cbd5e1);
  background-color: var(--tecnm-surface-white, #ffffff);
  color: var(--tecnm-text-primary, #0f172a);
  transition: border-color 0.2s ease, box-shadow 0.2s ease;
  width: 100%;
}

.tecnm-search-standard-input:focus {
  outline: none;
  border-color: var(--tecnm-blue, #1B396A);
  box-shadow: 0 0 0 3px rgba(27, 57, 106, 0.15);
}

.tecnm-toolbar-filters {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem;
}

.tecnm-toolbar-actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem;
  margin-left: auto;
}

@media (max-width: 768px) {
  .tecnm-search-standard-container {
    max-width: 100%;
    flex: 1 1 100%;
  }

  .tecnm-toolbar-filters,
  .tecnm-toolbar-actions {
    width: 100%;
    margin-left: 0;
  }
}
</style>
