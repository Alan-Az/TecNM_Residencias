<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useGlobalSearch } from '@/composables/useGlobalSearch'
import apiClient from '@/services/api'

const route = useRoute()
const authStore = useAuthStore()
const { open: openGlobalSearch } = useGlobalSearch()

const isStudent = computed(() =>
  authStore.hasRole('student') && !authStore.hasRole('admin', 'departmenthead', 'advisor')
)
const isAdvisor = computed(() =>
  authStore.hasRole('advisor') && !authStore.hasRole('admin', 'departmenthead')
)

const currentProject = ref(null)
const activities = ref([])
const isLoading = ref(false)
const errorMessage = ref('')
const alertMessage = ref('')
const alertType = ref('info')

const isProjectCompleted = computed(() => {
  const st = String(currentProject.value?.status || '').toLowerCase()
  return st === 'completed' || currentProject.value?.isCompleted === true
})

const isAccreditationProject = computed(() => {
  if (!currentProject.value) return false
  const t = String(currentProject.value?.projectType || '').toLowerCase()
  return t === 'acreditacion_innovatec' || t === 'acreditacion_hackatec'
})

const isAccreditationActive = computed(() => {
  if (!isAccreditationProject.value) return false
  const st = String(currentProject.value?.status || '').toLowerCase()
  return !['rejected', 'cancelled'].includes(st)
})

const isAccreditationUnderReview = computed(() => {
  if (!isAccreditationProject.value) return false
  const st = String(currentProject.value?.status || '').toLowerCase()
  return ['under_review', 'pending', 'proposed'].includes(st) && !currentProject.value?.reviewComments
})

const isAccreditationReturned = computed(() => {
  if (!isAccreditationProject.value) return false
  const st = String(currentProject.value?.status || '').toLowerCase()
  return (st === 'draft' || st === 'rejected') && !!currentProject.value?.reviewComments
})

const isAccreditationCompleted = computed(() => {
  if (!isAccreditationProject.value) return false
  const st = String(currentProject.value?.status || '').toLowerCase()
  return st === 'completed' || currentProject.value?.isCompleted === true
})

const isAccreditationDenied = computed(() => {
  if (!isAccreditationProject.value) return false
  const st = String(currentProject.value?.status || '').toLowerCase()
  return (st === 'rejected' || st === 'cancelled') && !currentProject.value?.reviewComments
})

const isProjectPending = computed(() => {
  const st = String(currentProject.value?.status || '').toLowerCase()
  return ['pending', 'proposed', 'under_review'].includes(st)
})

const isProjectDraft = computed(() => {
  const st = String(currentProject.value?.status || '').toLowerCase()
  return st === 'draft'
})

const isProjectRejected = computed(() => {
  const st = String(currentProject.value?.status || '').toLowerCase()
  return st === 'rejected'
})

const isProjectReadOnly = computed(() => {
  if (!isStudent.value) return true
  if (!currentProject.value) return true
  const st = String(currentProject.value?.status || '').toLowerCase()
  return isProjectCompleted.value || ['cancelled', 'rejected', 'pending', 'under_review', 'proposed', 'draft'].includes(st)
})

const MIN_WEEKS = 16
const MAX_WEEKS = 26
const visibleWeeksCount = ref(16)

const TOTAL_WEEKS = computed(() => {
  let maxUsed = visibleWeeksCount.value
  for (const act of activities.value || []) {
    for (const p of act.progresses || []) {
      if (p.status && p.status !== 'pending' && p.weekNumber > maxUsed) {
        maxUsed = p.weekNumber
      }
    }
  }
  return Math.min(MAX_WEEKS, Math.max(MIN_WEEKS, maxUsed))
})

const canAddActivity = computed(() => {
  if (!isStudent.value) return false
  if (!currentProject.value?.id) return false
  if (isAccreditationActive.value || isAccreditationCompleted.value) return false
  return !isProjectReadOnly.value
})

// Modal de agregar / editar actividad
const isModalOpen = ref(false)
const editingActivityId = ref(null)
const activityTitle = ref('')
const startWeek = ref(1)
const durationType = ref('weeks') // 'weeks' | 'days'
const durationValue = ref(2)
const isSubmitting = ref(false)

const effectiveWeeks = computed(() => {
  const val = Number(durationValue.value) || 1
  if (durationType.value === 'days') {
    return Math.max(1, Math.min(26, Math.ceil(val / 7)))
  }
  return Math.max(1, Math.min(26, val))
})

const effectiveDays = computed(() => {
  const val = Number(durationValue.value) || 1
  if (durationType.value === 'days') {
    return Math.max(1, val)
  }
  return val * 7
})

const rangeSummaryText = computed(() => {
  const start = Number(startWeek.value) || 1
  const end = Math.min(26, start + effectiveWeeks.value - 1)
  return `Semana ${start} a Semana ${end} (${effectiveWeeks.value} sem. / ~${effectiveDays.value} días)`
})

function showAlert(message, type = 'info') {
  alertMessage.value = message
  alertType.value = type
  setTimeout(() => {
    if (alertMessage.value === message) {
      alertMessage.value = ''
    }
  }, 5000)
}

const selectedProjectText = computed(() => {
  if (!currentProject.value) {
    return isStudent.value ? 'Sin anteproyecto activo' : 'Seleccione un anteproyecto'
  }
  const title = currentProject.value.title || currentProject.value.name || 'Anteproyecto'
  const student = currentProject.value.studentName || currentProject.value.student_name || ''
  const ctrl = currentProject.value.studentControlNumber || currentProject.value.student_control_number || ''
  const studentInfo = student ? ` (Alumno: ${student}${ctrl ? ' - ' + ctrl : ''})` : ''
  return `${title}${studentInfo}`
})

const staffProjects = ref([])
const staffSearchQuery = ref('')
const isStudentListLoading = ref(false)

function getBadgeClassForProject(p) {
  if (!p) return 'tecnm-badge-neutral'
  const st = String(p.status || '').toLowerCase()
  if (st === 'completed') return 'tecnm-badge-approved'
  if (st === 'in_progress') return 'tecnm-badge-pending'
  if (st === 'approved') return 'tecnm-badge-approved'
  if (st === 'pending' || st === 'under_review' || st === 'proposed') return 'tecnm-badge-pending'
  if (st === 'rejected' || st === 'cancelled') return 'tecnm-badge-rejected'
  return 'tecnm-badge-neutral'
}

function getStatusLabelForProject(p) {
  if (!p) return isStudent.value ? 'Sin anteproyecto' : 'No seleccionado'
  const st = String(p.status || '').toLowerCase()
  if (st === 'completed') return 'Concluido / Acreditado'
  if (st === 'in_progress') return 'En Desarrollo'
  if (st === 'approved') return 'Aprobado'
  if (st === 'pending' || st === 'under_review' || st === 'proposed') return 'En Revisión'
  if (st === 'draft') return 'Borrador'
  if (st === 'rejected') return 'Con Observaciones'
  if (st === 'cancelled') return 'Cancelado'
  return st
}

const projectStatusBadgeClass = computed(() => getBadgeClassForProject(currentProject.value))
const projectStatusLabel = computed(() => getStatusLabelForProject(currentProject.value))

const filteredStaffProjects = computed(() => {
  const q = staffSearchQuery.value.trim().toLowerCase()
  if (!q) return staffProjects.value
  return staffProjects.value.filter((p) => {
    const student = (p.studentName || p.student_name || '').toLowerCase()
    const ctrl = (p.studentControlNumber || p.student_control_number || '').toLowerCase()
    const title = (p.title || p.name || '').toLowerCase()
    const advisor = (p.advisorName || p.advisor_name || '').toLowerCase()
    const career = (p.careerName || p.career_name || '').toLowerCase()
    return student.includes(q) || ctrl.includes(q) || title.includes(q) || advisor.includes(q) || career.includes(q)
  })
})

async function returnToStudentList() {
  currentProject.value = null
  activities.value = []
  if (staffProjects.value.length === 0) {
    await loadInitialProjectForStaff()
  }
}

async function initSchedule() {
  if (route.query.projectId) {
    try {
      const res = await apiClient.get(`/v1/projects/${route.query.projectId}`)
      if (res.data?.id) {
        await selectProject(res.data)
        return
      }
    } catch (err) {
      console.warn('No se pudo cargar el proyecto solicitado por query params:', err)
    }
  }

  if (isStudent.value) {
    await resolveStudentProject()
  } else {
    await loadInitialProjectForStaff()
  }
}

async function resolveStudentProject() {
  isLoading.value = true
  errorMessage.value = ''
  try {
    const res = await apiClient.get('/v1/projects/me/current')
    if (res.data && res.data.id) {
      currentProject.value = res.data
      await loadSchedule(res.data.id)
    } else {
      currentProject.value = null
      activities.value = []
    }
  } catch (err) {
    currentProject.value = null
    activities.value = []
    if (err.response?.status === 404) {
      // Sin anteproyecto activo o registrado
    } else if (err.response?.status === 403) {
      errorMessage.value = 'El cronograma personal es exclusivo para estudiantes.'
    } else {
      errorMessage.value = 'Error al cargar el cronograma de actividades desde el servidor.'
    }
  } finally {
    isLoading.value = false
  }
}

const isEmptyState = ref(false)

async function loadInitialProjectForStaff() {
  isLoading.value = true
  isStudentListLoading.value = true
  errorMessage.value = ''
  isEmptyState.value = false
  try {
    const endpoint = isAdvisor.value
      ? '/v1/projects/advisor/me?pageSize=100'
      : '/v1/projects?pageSize=100'
    const res = await apiClient.get(endpoint)
    const rawData = res.data
    let list = Array.isArray(rawData)
      ? rawData
      : (rawData && Array.isArray(rawData.items) ? rawData.items : [])
    list = list.filter((p) => (p.status || '').toLowerCase() !== 'draft')
    staffProjects.value = list

    if (list.length === 0) {
      isEmptyState.value = true
      currentProject.value = null
      activities.value = []
      return
    }

    currentProject.value = null
    activities.value = []
  } catch (err) {
    if (err.response?.status === 404 || !err.response) {
      isEmptyState.value = true
    } else {
      errorMessage.value = 'Error al consultar anteproyectos asignados.'
    }
    staffProjects.value = []
    currentProject.value = null
    activities.value = []
  } finally {
    isLoading.value = false
    isStudentListLoading.value = false
  }
}

async function selectProject(project) {
  if (!project || !project.id) return
  currentProject.value = project
  errorMessage.value = ''
  await loadSchedule(project.id)

  // Enriquecer datos de estudiante si faltan
  if (!project.studentName && !project.student_name) {
    try {
      const res = await apiClient.get(`/v1/projects/${project.id}`)
      if (res.data) {
        currentProject.value = { ...project, ...res.data }
      }
    } catch {}
  }
}

async function loadSchedule(projectId) {
  if (!projectId) return
  isLoading.value = true
  errorMessage.value = ''
  try {
    const res = await apiClient.get(`/v1/projects/${projectId}/activities`)
    activities.value = Array.isArray(res.data) ? res.data : []
  } catch (err) {
    errorMessage.value = 'Error al cargar el cronograma de actividades desde la base de datos.'
    activities.value = []
  } finally {
    isLoading.value = false
  }
}

function openProjectPicker() {
  openGlobalSearch({
    initialSource: 'PROJECTS',
    onSelect: (item) => {
      if (item && item.id) {
        selectProject(item)
      }
    },
  })
}

function getWeekProgress(act, weekNum) {
  const progresses = act.progresses || []
  return progresses.find((p) => p.weekNumber === weekNum) || { status: 'pending' }
}

function getStatusClass(status) {
  const s = String(status || '').toLowerCase()
  if (s === 'completed' || s === 'completado') return 'completed'
  if (s === 'in_progress' || s === 'en_proceso' || s === 'en_progreso') return 'in_progress'
  return 'pending'
}


function getStatusLabelSpanish(status) {
  const s = String(status || '').toLowerCase()
  if (s === 'completed' || s === 'completado') return 'Completado'
  if (s === 'in_progress' || s === 'en_proceso' || s === 'en_progreso') return 'En Proceso'
  return 'Pendiente'
}

async function cycleWeekStatus(act, weekNum) {
  if (!currentProject.value?.id) return

  if (!isStudent.value) {
    showAlert('El cronograma de actividades solo puede ser editado por el alumno residente titular.', 'info')
    return
  }

  if (isAccreditationProject.value) {
    showAlert('El trámite de residencia por InnovaTecNM Nacional no requiere registro semanal de actividades en el cronograma.', 'info')
    return
  }

  if (isProjectReadOnly.value) {
    if (isProjectCompleted.value) {
      showAlert('Este proyecto se encuentra concluido. El cronograma está en modo solo lectura.', 'info')
    } else {
      showAlert('No se puede modificar el avance del cronograma en el estado actual del anteproyecto.', 'warning')
    }
    return
  }

  const currentProg = getWeekProgress(act, weekNum)
  const currentStatus = currentProg.status || 'pending'

  let nextStatus = 'in_progress'
  if (currentStatus === 'in_progress' || currentStatus === 'en_proceso' || currentStatus === 'en_progreso') {
    nextStatus = 'completed'
  } else if (currentStatus === 'completed' || currentStatus === 'completado') {
    nextStatus = 'pending'
  }

  // Actualización optimista local
  if (!act.progresses) act.progresses = []
  const existingIdx = act.progresses.findIndex((p) => p.weekNumber === weekNum)
  if (existingIdx >= 0) {
    act.progresses[existingIdx].status = nextStatus
  } else {
    act.progresses.push({ weekNumber: weekNum, status: nextStatus })
  }

  try {
    const res = await apiClient.post(
      `/v1/projects/${currentProject.value.id}/activities/progress`,
      {
        activityId: act.id,
        weekNumber: weekNum,
        status: nextStatus,
      }
    )
    if (res.status === 200 || res.status === 201 || res.status === 204) {
      const statusSpanish = getStatusLabelSpanish(nextStatus)
      showAlert(`Avance actualizado: Semana ${weekNum} (${statusSpanish}).`, 'success')
    }
  } catch (err) {
    // Revertir en caso de error
    if (existingIdx >= 0) {
      act.progresses[existingIdx].status = currentStatus
    }
    showAlert('Error al actualizar el avance semanal.', 'danger')
  }
}

function openAddModal() {
  if (!currentProject.value?.id) {
    showAlert('Debe seleccionar o registrar un anteproyecto activo primero.', 'warning')
    return
  }
  if (isProjectReadOnly.value) {
    showAlert('No se pueden agregar actividades a un proyecto concluido o en revisión.', 'warning')
    return
  }
  editingActivityId.value = null
  activityTitle.value = ''
  startWeek.value = 1
  durationType.value = 'weeks'
  durationValue.value = 2
  isModalOpen.value = true
}

function openEditModal(act) {
  if (isProjectReadOnly.value) {
    showAlert('No se puede modificar actividades de un proyecto concluido o en solo lectura.', 'warning')
    return
  }
  editingActivityId.value = act.id
  activityTitle.value = act.title || ''
  startWeek.value = act.startWeek || 1
  durationType.value = 'weeks'
  durationValue.value = act.estimatedWeeks || 2
  isModalOpen.value = true
}

function closeModal() {
  isModalOpen.value = false
  editingActivityId.value = null
  activityTitle.value = ''
  startWeek.value = 1
  durationType.value = 'weeks'
  durationValue.value = 2
}

async function handleSaveActivity() {
  if (!currentProject.value?.id) {
    showAlert('Debe seleccionar un anteproyecto válido.', 'warning')
    return
  }
  const title = activityTitle.value.trim()
  if (!title) {
    showAlert('Ingrese una descripción válida para la actividad.', 'warning')
    return
  }

  isSubmitting.value = true

  try {
    if (editingActivityId.value) {
      const payload = {
        title,
        startWeek: Number(startWeek.value) || 1,
        estimatedWeeks: effectiveWeeks.value,
        estimatedDays: effectiveDays.value,
      }
      await apiClient.put(`/v1/projects/${currentProject.value.id}/activities/${editingActivityId.value}`, payload)
      showAlert('¡Actividad y semanas estimadas actualizadas correctamente!', 'success')
    } else {
      const activityNumber = activities.value.length + 1
      const payload = {
        projectId: currentProject.value.id,
        activityNumber,
        title,
        startWeek: Number(startWeek.value) || 1,
        estimatedWeeks: effectiveWeeks.value,
        estimatedDays: effectiveDays.value,
      }
      await apiClient.post(`/v1/projects/${currentProject.value.id}/activities`, payload)
      showAlert('¡Actividad agregada correctamente al cronograma!', 'success')
    }

    closeModal()
    await loadSchedule(currentProject.value.id)
  } catch (err) {
    const msg = err.response?.data?.message || 'Error al guardar la actividad en el cronograma.'
    showAlert(msg, 'danger')
  } finally {
    isSubmitting.value = false
  }
}

onMounted(() => {
  initSchedule()
})
</script>

<template>
  <div>
    <!-- Barra de Título y Acciones -->
    <div class="tecnm-actions-bar">
      <div>
        <h1 class="tecnm-page-title">Cronograma de Actividades (16 - 26 Semanas)</h1>
        <p class="tecnm-page-subtitle">
          {{ !isStudent && !currentProject
              ? (authStore.isAdmin ? 'Supervisión global de cronogramas de residentes' : (isAdvisor ? 'Seguimiento de cronogramas de tus alumnos asignados' : 'Supervisión de cronogramas de la carrera'))
              : 'Planeación y seguimiento semanal de Residencia Profesional (Mínimo 16 semanas / 500 hrs)' }}
        </p>
      </div>
      <div class="tecnm-page-actions">
        <button
          v-if="!isStudent && currentProject"
          type="button"
          class="tecnm-btn tecnm-btn-secondary"
          @click="returnToStudentList"
        >
          ⬅️ Cambiar Alumno
        </button>
        <button
          v-if="!isStudent"
          type="button"
          class="tecnm-btn tecnm-btn-secondary"
          @click="openProjectPicker"
        >
          <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z" />
          </svg>
          <span>Buscar Alumno</span>
        </button>
        <button
          v-if="canAddActivity"
          id="addActivityBtn"
          type="button"
          class="tecnm-btn tecnm-btn-primary"
          @click="openAddModal"
        >
          + Nueva Actividad
        </button>
      </div>
    </div>

    <!-- Banner Contextual según Estado del Proyecto (Solo para Estudiante) -->
    <template v-if="isStudent">
      <!-- Casos para Modalidad InnovaTecNM Nacional -->
      <template v-if="isAccreditationProject">
        <div v-if="isAccreditationCompleted" class="tecnm-alert tecnm-alert-success" role="alert" style="margin-bottom: 1rem;">
          <span><strong>Residencia Profesional Acreditada y Liberada al 100% por InnovaTecNM Nacional:</strong> El cronograma semanal de 26 semanas se encuentra exento ya que tu proyecto fue evaluado y liberado con calificación máxima aprobatoria.</span>
        </div>
        <div v-else-if="isAccreditationReturned" class="tecnm-alert tecnm-alert-warning" role="alert" style="margin-bottom: 1rem;">
          <div class="tecnm-d-flex tecnm-justify-between tecnm-align-center tecnm-flex-wrap tecnm-gap-2">
            <span><strong>Constancia de InnovaTecNM Nacional con Observaciones:</strong> La Jefatura de Carrera solicitó correcciones (<em>{{ currentProject?.reviewComments }}</em>). Atiende las observaciones desde tu Panel Principal.</span>
            <router-link to="/dashboard" class="tecnm-btn tecnm-btn-primary tecnm-btn-sm">
              Ir al Panel Principal &rarr;
            </router-link>
          </div>
        </div>
        <div v-else-if="isAccreditationUnderReview" class="tecnm-alert tecnm-alert-info" role="alert" style="margin-bottom: 1rem;">
          <span><strong>Trámite de InnovaTecNM Nacional en Revisión:</strong> Tu constancia oficial se encuentra en análisis por la Jefatura. El registro de cronograma está pausado durante el análisis.</span>
        </div>
        <div v-else-if="isAccreditationDenied" class="tecnm-alert tecnm-alert-danger" role="alert" style="margin-bottom: 1rem;">
          <span><strong>Acreditación por InnovaTecNM No Aprobada:</strong> Tu constancia no fue validada. Las opciones para registrar un anteproyecto tradicional y habilitar tu cronograma han sido reactivadas.</span>
        </div>
      </template>

      <!-- Casos para Flujo Ordinario Tradicional -->
      <template v-else>
        <div v-if="isProjectCompleted" class="tecnm-alert tecnm-alert-success" role="alert" style="margin-bottom: 1rem;">
          <span><strong>Residencia Profesional Concluida y Acreditada:</strong> El cronograma se encuentra en modo histórico de solo lectura.</span>
        </div>
        <div v-else-if="isProjectPending" class="tecnm-alert tecnm-alert-warning" role="alert" style="margin-bottom: 1rem;">
          <span><strong>Anteproyecto en Dictamen:</strong> Tu solicitud se encuentra en revisión por la Academia/División. Una vez dictaminada favorablemente, podrás capturar actividades.</span>
        </div>
        <div v-else-if="isProjectDraft" class="tecnm-alert tecnm-alert-info" role="alert" style="margin-bottom: 1rem;">
          <span><strong>Anteproyecto en Borrador:</strong> Envía tu solicitud a revisión en el módulo de <router-link to="/projects/proposal"><strong>Solicitud de Anteproyecto</strong></router-link> para habilitar tu cronograma.</span>
        </div>
        <div v-else-if="isProjectRejected" class="tecnm-alert tecnm-alert-danger" role="alert" style="margin-bottom: 1rem;">
          <span><strong>Anteproyecto con Observaciones:</strong> Realiza las correcciones solicitadas en el módulo de <router-link to="/projects/proposal"><strong>Solicitud de Anteproyecto</strong></router-link>.</span>
        </div>
      </template>

      <div v-if="!currentProject && !isLoading" class="tecnm-alert tecnm-alert-info" role="alert" style="margin-bottom: 1rem; display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 0.5rem;">
        <span><strong>Sin Anteproyecto Registrado:</strong> Aún no cuentas con un anteproyecto registrado en el sistema.</span>
        <router-link to="/projects/proposal" class="tecnm-btn tecnm-btn-primary tecnm-btn-sm">
          + Registrar Solicitud de Anteproyecto
        </router-link>
      </div>
    </template>
    <div v-else-if="!isStudent && currentProject" class="tecnm-alert tecnm-alert-info" role="alert" style="margin-bottom: 1rem;">
      <span><strong>Modo Consulta (Solo Lectura):</strong> El cronograma de actividades y el registro de avance semanal son de edición única y exclusiva del alumno residente titular.</span>
    </div>

    <!-- Alert de Notificación Flotante Institucional -->
    <div
      v-if="alertMessage"
      id="alertContainer"
      class="tecnm-alert"
      :class="`tecnm-alert-${alertType}`"
      role="alert"
    >
      <span>{{ alertMessage }}</span>
      <button
        type="button"
        class="tecnm-alert-close"
        aria-label="Cerrar"
        @click="alertMessage = ''"
      >
        &times;
      </button>
    </div>

    <!-- 1. VISTA LISTA DE ALUMNOS (PARA STAFF / DOCENTES CUANDO NO HAY ALUMNO SELECCIONADO) -->
    <div v-if="!isStudent && !currentProject" class="tecnm-card">
      <div class="tecnm-card-header tecnm-d-flex tecnm-justify-between tecnm-align-center">
        <div>
          <h3 class="tecnm-card-title">
            {{ authStore.isAdmin ? 'Directorio de Alumnos Residentes' : (isAdvisor ? 'Mis Alumnos Residentes Asignados' : 'Alumnos Residentes de la Carrera') }}
          </h3>
          <p class="tecnm-card-subtitle" style="margin: 0.25rem 0 0; font-size: 0.85rem; color: #64748b;">
            Selecciona a un alumno para consultar, revisar o dar seguimiento a su cronograma de 26 semanas.
          </p>
        </div>
      </div>

      <div class="tecnm-card-toolbar">
        <div style="flex: 1; max-width: 420px;">
          <input
            v-model="staffSearchQuery"
            type="text"
            class="tecnm-form-control tecnm-form-control-sm"
            placeholder="Filtrar por alumno, no. control, carrera o anteproyecto..."
          />
        </div>
        <div class="tecnm-toolbar-actions">
          <button
            type="button"
            class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
            @click="openProjectPicker"
          >
            <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z" />
            </svg>
            <span>Buscar en Catálogo Global</span>
          </button>
        </div>
      </div>

      <div class="tecnm-card-body">
        <div class="tecnm-table-responsive">
          <table class="tecnm-table tecnm-table-striped">
            <thead>
              <tr>
                <th>No. Control</th>
                <th>Estudiante</th>
                <th>Carrera</th>
                <th>Anteproyecto</th>
                <th v-if="authStore.isAdmin">Asesor</th>
                <th>Estado</th>
                <th class="tecnm-th-actions">Acción</th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="isStudentListLoading">
                <td :colspan="authStore.isAdmin ? 7 : 6" class="tecnm-table-empty">
                  Cargando alumnos asignados...
                </td>
              </tr>
              <tr v-else-if="filteredStaffProjects.length === 0">
                <td :colspan="authStore.isAdmin ? 7 : 6" class="tecnm-table-empty">
                  {{ staffSearchQuery ? 'No se encontraron alumnos que coincidan con la búsqueda.' : (isAdvisor ? 'No tienes alumnos residentes asignados con anteproyecto activo actualmente.' : 'No se registran alumnos con anteproyectos activos.') }}
                </td>
              </tr>
              <tr
                v-for="p in filteredStaffProjects"
                v-else
                :key="p.id"
              >
                <td><strong>{{ p.studentControlNumber || p.student_control_number || '—' }}</strong></td>
                <td>{{ p.studentName || p.student_name || 'Estudiante' }}</td>
                <td>{{ p.careerName || p.career_name || '—' }}</td>
                <td>
                  <div style="font-weight: 500;">{{ p.title || p.name || 'Anteproyecto' }}</div>
                  <div v-if="p.companyName || p.company_name" style="font-size: 0.75rem; color: #64748b;">
                    🏢 {{ p.companyName || p.company_name }}
                  </div>
                </td>
                <td v-if="authStore.isAdmin">{{ p.advisorName || p.advisor_name || 'Sin asesor' }}</td>
                <td>
                  <span class="tecnm-badge" :class="getBadgeClassForProject(p)">
                    {{ getStatusLabelForProject(p) }}
                  </span>
                </td>
                <td>
                  <button
                    type="button"
                    class="tecnm-btn tecnm-btn-primary tecnm-btn-sm"
                    @click="selectProject(p)"
                  >
                    Ver Cronograma &rarr;
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- 2. TARJETA PRINCIPAL CON MATRIZ DE 26 SEMANAS (CUANDO HAY PROYECTO SELECCIONADO O ES ESTUDIANTE) -->
    <div v-else class="tecnm-card">
      <div class="tecnm-card-header tecnm-d-flex tecnm-justify-between tecnm-align-center">
        <div class="tecnm-d-flex tecnm-align-center tecnm-gap-2 tecnm-flex-wrap">
          <h3 class="tecnm-card-title">Matriz de Avance Semanal</h3>
          <div id="projectSearchContainer" class="tecnm-d-flex tecnm-align-center tecnm-gap-2">
            <button
              v-if="!isStudent"
              type="button"
              class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
              @click="returnToStudentList"
            >
              ⬅️ Cambiar Alumno
            </button>
            <button
              v-if="!isStudent"
              id="searchProjectBtn"
              type="button"
              class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
              @click="openProjectPicker"
            >
              <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                <path stroke-linecap="round" stroke-linejoin="round" d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z" />
              </svg>
              <span>Buscar Anteproyecto</span>
            </button>
            <span v-if="currentProject" id="selectedProjectBadge" class="tecnm-badge" :class="projectStatusBadgeClass">
              {{ selectedProjectText }} — [{{ projectStatusLabel }}]
            </span>
          </div>
        </div>

        <div class="tecnm-legend tecnm-d-flex tecnm-align-center tecnm-gap-2" style="flex-wrap: wrap;">
          <div class="tecnm-d-flex tecnm-align-center tecnm-gap-1">
            <label class="tecnm-label tecnm-mb-0" style="font-size: 0.8rem; color: var(--tecnm-text-muted); font-weight: 500;">Vista:</label>
            <select v-model.number="visibleWeeksCount" class="tecnm-form-control tecnm-form-control-sm" style="width: auto; padding: 0.15rem 0.4rem; font-size: 0.8rem;">
              <option :value="16">16 Semanas (Mínimo TecNM)</option>
              <option :value="20">20 Semanas</option>
              <option :value="24">24 Semanas</option>
              <option :value="26">26 Semanas (Completo)</option>
            </select>
          </div>
          <span>Leyenda:</span>
          <span class="tecnm-badge tecnm-badge-approved">Completado</span>
          <span class="tecnm-badge tecnm-badge-pending">En Proceso</span>
          <span class="tecnm-badge tecnm-badge-neutral">Pendiente</span>
        </div>
      </div>

      <div class="tecnm-card-body">
        <div class="tecnm-table-responsive">
          <table class="matrix-table tecnm-table" :class="{ 'readonly-matrix': !isStudent || isProjectReadOnly }">
            <thead>
              <tr>
                <th>#</th>
                <th class="act-title-col">Descripción de la Actividad</th>
                <th
                  v-for="w in TOTAL_WEEKS"
                  :key="w"
                  class="matrix-week-header"
                  :title="`Semana ${w}`"
                >
                  S{{ w }}
                </th>
              </tr>
            </thead>
            <tbody id="scheduleTableBody">
              <tr v-if="isLoading">
                <td :colspan="TOTAL_WEEKS + 2" class="tecnm-table-empty">
                  Cargando cronograma de actividades...
                </td>
              </tr>
              <tr v-else-if="isEmptyState || !currentProject">
                <td :colspan="TOTAL_WEEKS + 2" style="padding: 3rem 1.5rem; text-align: center;">
                  <div style="max-width: 500px; margin: 0 auto; display: flex; flex-direction: column; align-items: center; gap: 0.75rem;">
                    <div style="width: 54px; height: 54px; border-radius: 50%; background: #e0f2fe; color: #0284c7; display: flex; align-items: center; justify-content: center;">
                      <svg xmlns="http://www.w3.org/2000/svg" width="28" height="28" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.8">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M6.75 3v2.25M17.25 3v2.25M3 18.75V7.5a2.25 2.25 0 0 1 2.25-2.25h13.5A2.25 2.25 0 0 1 21 7.5v11.25m-18 0A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75m-18 0v-7.5A2.25 2.25 0 0 1 5.25 9h13.5A2.25 2.25 0 0 1 21 11.25v7.5" />
                      </svg>
                    </div>
                    <h4 style="margin: 0; font-size: 1.1rem; font-weight: 600; color: #1e293b;">
                      {{ authStore.isCareerHead ? 'Sin anteproyectos activos en tu carrera' : 'Sin anteproyecto seleccionado' }}
                    </h4>
                    <p style="margin: 0; font-size: 0.875rem; color: #64748b; line-height: 1.4;">
                      {{ authStore.isCareerHead
                        ? 'No se registran anteproyectos vigentes de estudiantes de tu carrera con cronograma activo. Los proyectos aprobados por la academia se reflejarán automáticamente aquí.'
                        : 'No se encontraron anteproyectos asignados. Puedes buscar manualmente un anteproyecto para consultar o registrar su cronograma.' }}
                    </p>
                    <button
                      v-if="!isStudent"
                      type="button"
                      class="tecnm-btn tecnm-btn-secondary tecnm-btn-sm"
                      style="margin-top: 0.5rem;"
                      @click="openProjectPicker"
                    >
                      <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                        <path stroke-linecap="round" stroke-linejoin="round" d="m21 21-5.197-5.197m0 0A7.5 7.5 0 1 0 5.196 5.196a7.5 7.5 0 0 0 10.607 10.607Z" />
                      </svg>
                      <span>Buscar Anteproyecto</span>
                    </button>
                  </div>
                </td>
              </tr>
              <tr v-else-if="errorMessage">
                <td :colspan="TOTAL_WEEKS + 2" class="tecnm-table-empty tecnm-text-danger">
                  {{ errorMessage }}
                </td>
              </tr>
              <tr v-else-if="!currentProject">
                <td :colspan="TOTAL_WEEKS + 2" class="tecnm-table-empty">
                  <p style="margin-bottom: 0.5rem;">No tienes un anteproyecto registrado para consultar el cronograma.</p>
                  <router-link v-if="isStudent" to="/projects/proposal" class="tecnm-btn tecnm-btn-primary tecnm-btn-sm">
                    Registrar Solicitud de Anteproyecto
                  </router-link>
                </td>
              </tr>
              <tr v-else-if="activities.length === 0">
                <td :colspan="TOTAL_WEEKS + 2" class="tecnm-table-empty">
                  <span v-if="isProjectCompleted">No se registraron actividades en este proyecto concluido.</span>
                  <span v-else-if="isProjectPending">El cronograma se habilitará una vez dictaminado favorablemente el anteproyecto.</span>
                  <span v-else>No hay actividades registradas en el cronograma. Haga clic en "+ Nueva Actividad".</span>
                </td>
              </tr>
              <tr
                v-for="act in activities"
                v-else
                :key="act.id"
              >
                <td><strong>{{ act.activityNumber }}</strong></td>
                <td class="act-title-col">
                  <div style="display: flex; align-items: center; justify-content: space-between; gap: 0.5rem;">
                    <span>{{ act.title }}</span>
                    <button
                      v-if="isStudent && !isProjectReadOnly"
                      type="button"
                      class="tecnm-btn tecnm-btn-secondary tecnm-btn-xs"
                      style="padding: 2px 6px; font-size: 0.75rem; white-space: nowrap;"
                      title="Editar actividad y duración estimada"
                      @click.stop="openEditModal(act)"
                    >
                      ✏️ Editar
                    </button>
                  </div>
                </td>
                <td
                  v-for="w in TOTAL_WEEKS"
                  :key="w"
                  class="week-cell"
                  :class="[getStatusClass(getWeekProgress(act, w).status), { 'readonly': !isStudent || isProjectReadOnly }]"
                  :data-activity-id="act.id"
                  :data-week="w"
                  :data-status="getWeekProgress(act, w).status"
                  :title="(!isStudent || isProjectReadOnly) ? `Actividad: ${act.title} - Semana ${w} (${getStatusLabelSpanish(getWeekProgress(act, w).status)}) [Solo lectura]` : `Actividad: ${act.title} - Semana ${w} (${getStatusLabelSpanish(getWeekProgress(act, w).status)}) - Haga clic para cambiar`"
                  @click="cycleWeekStatus(act, w)"
                >
                  <svg v-if="getStatusClass(getWeekProgress(act, w).status) === 'completed'" xmlns="http://www.w3.org/2000/svg" width="12" height="12" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="3" style="display: block; margin: auto;">
                    <path stroke-linecap="round" stroke-linejoin="round" d="m4.5 12.75 6 6 9-13.5" />
                  </svg>
                  <span v-else-if="getStatusClass(getWeekProgress(act, w).status) === 'in_progress'" style="display: block; width: 6px; height: 6px; border-radius: 50%; background-color: currentColor; margin: auto;"></span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- Modal Registrar / Editar Actividad -->
    <div
      id="createActivityModal"
      class="modal-backdrop"
      :class="{ active: isModalOpen }"
      aria-modal="true"
      role="dialog"
    >
      <div class="modal-card">
        <div class="tecnm-modal-header">
          <h3 class="tecnm-modal-title">
            {{ editingActivityId ? 'Editar Actividad del Cronograma' : 'Agregar Nueva Actividad al Cronograma' }}
          </h3>
          <button
            id="closeActivityModalBtn"
            type="button"
            class="tecnm-modal-close"
            aria-label="Cerrar"
            @click="closeModal"
          >
            &times;
          </button>
        </div>

        <form id="activityForm" @submit.prevent="handleSaveActivity">
          <div class="tecnm-form-group">
            <label for="activityTitleInput" class="tecnm-label">Descripción de la Actividad *</label>
            <input
              id="activityTitleInput"
              v-model="activityTitle"
              type="text"
              class="tecnm-form-control"
              placeholder="Ej. Análisis de requerimientos y diseño de base de datos..."
              required
            />
          </div>

          <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 1rem;">
            <div class="tecnm-form-group">
              <label for="startWeekSelect" class="tecnm-label">Semana de Inicio</label>
              <select
                id="startWeekSelect"
                v-model.number="startWeek"
                class="tecnm-form-control"
              >
                <option v-for="w in 26" :key="w" :value="w">
                  Semana {{ w }}
                </option>
              </select>
            </div>

            <div class="tecnm-form-group">
              <label for="durationTypeSelect" class="tecnm-label">Unidad de Estimación</label>
              <select
                id="durationTypeSelect"
                v-model="durationType"
                class="tecnm-form-control"
              >
                <option value="weeks">Semanas</option>
                <option value="days">Días hábiles</option>
              </select>
            </div>
          </div>

          <div class="tecnm-form-group">
            <label for="durationValueInput" class="tecnm-label">
              {{ durationType === 'weeks' ? 'Número de Semanas Estimadas' : 'Número de Días Estimados' }}
            </label>
            <input
              id="durationValueInput"
              v-model.number="durationValue"
              type="number"
              class="tecnm-form-control"
              :min="1"
              :max="durationType === 'weeks' ? 26 : 180"
              required
            />
          </div>

          <div style="background-color: var(--color-surface-subtle, #f8fafc); border: 1px solid var(--color-border, #e2e8f0); border-radius: 6px; padding: 0.75rem 1rem; margin-bottom: 1.25rem; font-size: 0.85rem;">
            <strong style="color: var(--color-text, #334155);">Rango programado:</strong>
            <span style="color: var(--color-primary, #0284c7); margin-left: 0.5rem; font-weight: 600;">
              {{ rangeSummaryText }}
            </span>
          </div>

          <div class="tecnm-modal-footer">
            <button
              id="cancelActivityModalBtn"
              type="button"
              class="tecnm-btn tecnm-btn-secondary"
              @click="closeModal"
            >
              Cancelar
            </button>
            <button
              id="submitActivityBtn"
              type="submit"
              class="tecnm-btn tecnm-btn-primary"
              :disabled="isSubmitting"
            >
              {{ isSubmitting ? 'Guardando...' : (editingActivityId ? 'Actualizar Actividad' : 'Guardar Actividad') }}
            </button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>
