<script setup>
import { ref, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const router = useRouter()
const route = useRoute()
const authStore = useAuthStore()

const isLoading = ref(false)
const errorMessage = ref('')
const blockDetails = ref(null)
const ssoConfig = ref({
  loginPortalUrl: 'http://localhost:5173/login',
  publicBaseUrl: 'http://localhost:5140',
})

function parseBlockReason(message) {
  if (!message) return null

  const requirements = {
    'Actividades Complementarias': message.includes('Actividades Complementarias'),
    'Servicio Social': message.includes('Servicio Social'),
    'Especiales': message.includes('Especiales') || message.includes('Requisitos Especiales'),
  }

  const failedRequirements = Object.entries(requirements)
    .filter(([, failed]) => failed)
    .map(([name]) => name)

  const isManualBlock = message.includes('[Bloqueo manual]')
  const cleanMessage = message.replace('[Bloqueo manual] ', '')

  return {
    isManualBlock,
    failedRequirements,
    message: cleanMessage,
  }
}

function generateRandomState() {
  const bytes = new Uint8Array(16)
  window.crypto.getRandomValues(bytes)
  return Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('')
}

function decodeBase64UrlJson(encoded) {
  const base64 = encoded.replace(/-/g, '+').replace(/_/g, '/')
  const padded = base64 + '='.repeat((4 - (base64.length % 4)) % 4)
  const binary = window.atob(padded)
  const bytes = Uint8Array.from(binary, (c) => c.charCodeAt(0))
  const jsonStr = new TextDecoder().decode(bytes)
  return JSON.parse(jsonStr)
}

function handlePortalRedirect() {
  errorMessage.value = ''
  blockDetails.value = null
  window.location.href = ssoConfig.value.loginPortalUrl || 'http://localhost:5173/login'
}

onMounted(async () => {
  try {
    const cfg = await authStore.getSsoConfig()
    if (cfg) {
      ssoConfig.value = {
        loginPortalUrl: cfg.loginPortalUrl || 'http://localhost:5173/login',
        publicBaseUrl: cfg.publicBaseUrl || 'http://localhost:5140',
      }
    }
  } catch {
    // Mantiene URLs por defecto si la consulta de configuración falla
  }

  const ssoError = typeof route.query.sso_error === 'string' ? route.query.sso_error : ''
  const ssoPayload = typeof route.query.sso_payload === 'string' ? route.query.sso_payload : ''
  const ticket = typeof route.query.ticket === 'string' ? route.query.ticket : ''
  const code = typeof route.query.code === 'string' ? route.query.code : ''
  const state = typeof route.query.state === 'string' ? route.query.state : ''

  // Caso A: Error devuelto por el backend tras /acceso
  if (ssoError) {
    blockDetails.value = parseBlockReason(ssoError)
    errorMessage.value = ssoError
    return
  }

  // Caso B: Payload autenticado devuelto por el backend tras /acceso
  if (ssoPayload) {
    isLoading.value = true
    try {
      const authData = decodeBase64UrlJson(ssoPayload)
      await authStore.setSessionData(authData)
      router.replace('/dashboard')
    } catch {
      errorMessage.value = 'No fue posible completar el inicio de sesión SSO. Intenta nuevamente desde el portal.'
    } finally {
      isLoading.value = false
    }
    return
  }

  // Caso C: Handshake Fase 1 en Frontend (?ticket=...)
  if (ticket && !code) {
    isLoading.value = true
    const generatedState = generateRandomState()
    sessionStorage.setItem('rtecnm_sso_state', generatedState)
    const base = (ssoConfig.value.publicBaseUrl || 'http://localhost:5140').replace(/\/$/, '')
    window.location.replace(
      `${base}/api/modules/authorize?ticket=${encodeURIComponent(ticket.trim())}&state=${encodeURIComponent(generatedState)}`
    )
    return
  }

  // Caso D: Handshake Fase 2 en Frontend (?code=...&state=...)
  if (code) {
    isLoading.value = true
    try {
      const expectedState = sessionStorage.getItem('rtecnm_sso_state')
      sessionStorage.removeItem('rtecnm_sso_state')
      if (expectedState && expectedState !== state) {
        errorMessage.value = 'Estado SSO inválido o expirado. Ingresa nuevamente desde el portal institucional.'
        return
      }

      await authStore.exchangeSsoCode(code.trim(), state || null)
      const redirectPath = typeof route.query.redirect === 'string' ? route.query.redirect : '/dashboard'
      router.replace(redirectPath)
    } catch (error) {
      const msg =
        error.response?.data?.message ||
        'No fue posible validar tu acceso institucional. Contacta a División de Estudios Profesionales.'
      if (error.response?.status === 403) {
        blockDetails.value = parseBlockReason(msg)
      }
      errorMessage.value = msg
    } finally {
      isLoading.value = false
    }
  }
})
</script>

<template>
  <div class="login-main">
    <div class="login-card">
      <div class="login-icon">
        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="1.5" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" d="M16.5 10.5V6.75a4.5 4.5 0 1 0-9 0v3.75m-.75 11.25h10.5a2.25 2.25 0 0 0 2.25-2.25v-6.75a2.25 2.25 0 0 0-2.25-2.25H6.75a2.25 2.25 0 0 0-2.25 2.25v6.75a2.25 2.25 0 0 0 2.25 2.25Z" />
        </svg>
      </div>

      <h1 class="login-title">Sistema de Residencias Profesionales TecNM</h1>
      <p class="login-subtitle">Acceso Institucional con Cuenta Microsoft (@monclova.tecnm.mx)</p>

      <div
        v-if="errorMessage"
        id="loginAlert"
        class="tecnm-alert tecnm-alert-danger login-alert visible"
        role="alert"
      >
        <span id="loginAlertMessage">{{ errorMessage }}</span>

        <div v-if="blockDetails && !blockDetails.isManualBlock && blockDetails.failedRequirements.length > 0" class="block-details mt-3">
          <h4 class="block-details-title">Requisitos no cumplidos:</h4>
          <ul class="block-requirements-list">
            <li v-for="req in blockDetails.failedRequirements" :key="req" class="block-requirement-item">
              <span class="requirement-icon">✗</span>
              <span class="requirement-name">{{ req }}</span>
            </li>
          </ul>
          <p class="block-details-note">Debe regularizar su situación académica para acceder al sistema.</p>
        </div>

        <div v-else-if="blockDetails && blockDetails.isManualBlock" class="block-details mt-3">
          <h4 class="block-details-title">Cuenta bloqueada por administrador:</h4>
          <p class="block-manual-reason">{{ blockDetails.message }}</p>
          <p class="block-details-note">Contacte a la administración para resolver esta situación.</p>
        </div>
      </div>

      <div v-if="isLoading" class="text-center py-4">
        <span class="login-spinner" style="display:inline-block;margin-bottom:0.75rem;"></span>
        <p class="text-sm text-gray-600">Validando credenciales institucionales con Microsoft TecNM...</p>
      </div>

      <div v-else class="login-form">
        <p class="text-sm text-gray-600 text-center mb-4">
          Para ingresar a Residencias Profesionales, inicia sesión a través del Portal Central de Control de Acceso con tu correo <strong>@monclova.tecnm.mx</strong>.
        </p>

        <button
          id="loginSsoBtn"
          type="button"
          class="tecnm-btn tecnm-btn-primary login-btn"
          @click="handlePortalRedirect"
        >
          Iniciar Sesión con Microsoft (@monclova.tecnm.mx)
        </button>
      </div>
    </div>
  </div>
</template>
