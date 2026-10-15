# Plan de Implementación: Estandarización del Diseño de Tablas, Filtros, Búsqueda y Exportación

**Sistema de Gestión de Residencias Profesionales TecNM v2**  
*Documento de especificación técnica de alta fidelidad y ruta de ejecución paso a paso blindada contra colisiones reactivas y desfasajes de datos.*

---

## 1. Diagnóstico Real y Problemas Identificados

Tras una inspección directa del código fuente en `RTecNM_V2_Frontend` y `RTecNM_V2_Backend`, se detectaron las siguientes inconsistencias visuales y arquitectónicas:

1. **Barras de Búsqueda Heterogéneas y Riesgos de Concurrencia**:
   - Dispersión de dimensiones aplicadas mediante estilos inline: `max-width: 420px` en `StudentsView` y `AdvisorsView`; `min-width: 260px` en `ReviewView`; `min-width: 250px` en `DocumentsView`; sin límite en `RolesView`.
   - Comportamientos mezclados: unas vistas ejecutan búsqueda reactiva con *debounce* manual mediante `setTimeout` (`StudentsView`, `AdvisorsView`, `ReviewView`), otras dependen de presionar Enter o botones manuales "Buscar" (`CompaniesView`, `CareersView`), y `ReportsView` carece de búsqueda textual.
   - **Riesgo detectado**: Al interactuar con el buscador global (`GlobalSearchModal`), el término de búsqueda se inyecta programáticamente. Si un componente de barra de herramientas introduce un *debounce* desalineado, puede generar dobles llamadas a la API (*race conditions*) o sobreescritura de filtros.

2. **Filtros Desalineados y Selectores Dispares**:
   - Selectores de Carrera, Asesor y Estatus con alturas, radios de borde y anchos mínimos no estandarizados (`min-width: 180px` a `230px`).
   - Interruptores (*toggles*) de "Mostrar inactivos", "Mostrar cancelados" u "Ocultar evaluados" ubicados en posiciones inconsistentes según la vista.

3. **Ordenamiento de Columnas (`th`) y Desfases Cliente/Servidor**:
   - Coexisten dos paradigmas de ordenamiento:
     - **Server-Side**: La tabla solicita datos ordenados a la base de datos vía API (`sortBy`, `sortDir`). Cada cambio de columna **debe obligatoriamente reiniciar la página a 1** (`pageNumber = 1`), de lo contrario el usuario queda varado en páginas vacías o desfasadas.
     - **Client-Side (En Memoria)**: Vistas como `CoordinatorDocumentDeadlines.vue` ordenan una lista fija en el cliente.
   - Vistas que ya cuentan con ordenamiento funcional (`CareersView.vue`, `CoordinatorDocumentDeadlines.vue`), pero utilizan iconografía dispar (`▲/▼` vs `↑/↓`) o estilos locales *scoped* que rompen la homogeneidad.
   - Endpoints de backend que reciben paginación pero ignoran el ordenamiento: `GetReleasableProjectsAsync` en `Admin/ReportGeneratorService.cs`, `/v1/notifications/history` en `NotificationRepository.cs`, y la matriz general de `/v1/documents/matrix`.
   - Sensibilidad de mayúsculas/minúsculas en C#: `CompanyRepository.cs` valida estrictamente nombres en PascalCase (`"Name"`, `"LegalName"`, `"Rfc"`, `"Sector"`). Enviar nombres en minúsculas provoca que el backend descarte el orden y aplique el fallback predeterminado.

4. **Exportación / Descarga de Tablas (Punto Crítico de Seguridad y RBAC)**:
   - **Rol Alumno (`student`)**: No debe tener acceso a herramientas de descarga masiva ni a tablas administrativas. Los estudiantes únicamente gestionan su trámite individual (anteproyecto, cronograma de actividades, reportes y descarga de su propia Solicitud de Residencia o Carta de Aceptación).
   - **Personal / Staff (`admin`, `vinculacion`, `departmenthead`, `academic`, `director`, `jefecarrera`, `careerhead`, `coordinadora`, `coordinator`)**: Requiere descargas uniformes (PDF y/o Excel) en todas las tablas maestras.
   - `CompaniesView` carece actualmente de exportación a PDF, y `ReportsView` carece tanto de exportación en backend como de procesamiento de filtros de búsqueda.

---

## 2. Principios de Diseño, Reactividad y Políticas de Acceso (RBAC)

### 2.1 Estandarización de Getters en Pinia (`stores/auth.js`)
Para evitar que los nuevos componentes consuman propiedades inexistentes o `undefined`, se debe blindar previamente `RTecNM_V2_Frontend/src/stores/auth.js`:

```javascript
// En stores/auth.js:
const isStudent = computed(() => {
  if (!currentRole.value) return false
  return (
    currentRole.value === 'student' ||
    (hasRole('student') &&
      !hasRole(
        'admin',
        'departmenthead',
        'advisor',
        'vinculacion',
        'director',
        'coordinadora',
        'coordinator',
        'jefecarrera',
        'careerhead',
        'academic'
      ))
  )
})

const isStaff = computed(() => {
  return !isStudent.value && Boolean(currentRole.value)
})
```

* Las barras de herramientas con descargas masivas (`TecnmExportButton`), interruptores de auditoría inactiva y filtros globales de carrera **no deben renderizarse** para el rol `student` (`v-if="authStore.isStaff"` o `v-if="!authStore.isStudent"`).

### 2.2 Especificación Visual TecNM (Design Tokens)
* **Buscador Estándar**: Ancho fijo `max-width: 360px`, altura `2.375rem` (`38px`), *debounce* automático de 350 ms, tipo `search` con botón de limpieza nativo (`x`) y sincronización reactiva bidireccional.
* **Selectores de Filtro**: Altura uniforme `2.375rem`, `min-width: 200px`, tipografía `0.85rem`, borde institucional `var(--tecnm-border-color)`.
* **Toolbar de Tabla**: Contenedor `.tecnm-card-toolbar` (`display: flex; flex-wrap: wrap; gap: 0.75rem; align-items: center;`). Buscador a la izquierda, filtros en el centro, toggles y exportación alineados a la derecha (`margin-left: auto`).

---

## 3. Nuevas Primitivas y Componentes Frontend

### 3.1 Composable `useTableSort.js`
Ubicación: `RTecNM_V2_Frontend/src/composables/useTableSort.js`

Centraliza la reactividad y la lógica de ordenamiento soportando tanto modo servidor como modo cliente, garantizando la preservación de PascalCase para C#:

```javascript
import { ref } from 'vue'

export function useTableSort(defaultSortBy = 'Id', defaultSortDir = 'asc', onSortChange = null) {
  const sortBy = ref(defaultSortBy)
  const sortDir = ref(defaultSortDir)

  function toggleSort(column) {
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
    if (sortBy.value.toLowerCase() !== column.toLowerCase()) return '↕'
    return sortDir.value === 'asc' ? '↑' : '↓'
  }

  function isSortActive(column) {
    return sortBy.value.toLowerCase() === column.toLowerCase()
  }

  return { sortBy, sortDir, toggleSort, getSortIcon, isSortActive }
}
```

* **Regla de oro para Server-Side**: En el callback `onSortChange` de la vista consumidora, se debe ejecutar siempre:
  ```javascript
  () => {
    pageNumber.value = 1
    loadData({ silent: true })
  }
  ```

### 3.2 Componente `TecnmTableToolbar.vue`
Ubicación: `RTecNM_V2_Frontend/src/components/common/TecnmTableToolbar.vue`

Estructura estandarizada con slots, blindada contra bucles de reactividad y *race conditions*:
* **Props**:
  - `modelValue` / `search`: String para el término de búsqueda.
  - `searchPlaceholder`: Texto guía (por defecto `"Buscar..."`).
  - `showSearch`: Booleano para ocultar el buscador si la vista no lo requiere.
* **Comportamiento del Buscador**:
  - Aplica un *debounce* interno de 350 ms sobre el evento de escritura del usuario antes de emitir `@search(value)` y `update:modelValue`.
  - Si el valor de `modelValue` cambia desde el componente padre (por ejemplo, al seleccionar un resultado en `GlobalSearchModal` o limpiar filtros), actualiza el input interno sin re-disparar el temporizador de debounce.
* **Slots Estructurados**:
  - `#filters`: Espacio para selects de Carrera, Asesor, Estatus o Departamento.
  - `#actions`: Espacio para toggles ("Mostrar inactivos", "Ocultar evaluados"), `TecnmExportButton` y botones de alta (`+ Nuevo`).

### 3.3 Componente `TecnmExportButton.vue`
Ubicación: `RTecNM_V2_Frontend/src/components/common/TecnmExportButton.vue`

* **Props**:
  - `loading`: Booleano (deshabilita el botón y muestra spinner/texto de carga).
  - `disabled`: Booleano.
  - `formats`: Array (`['pdf']`, `['excel']` o `['pdf', 'excel']`). Si incluye ambos, despliega un menú desplegable institucional accesible; si incluye uno solo, actúa como botón directo.
* **Eventos**:
  - `@export(format)`: Emite `'pdf'` o `'excel'`.
* **Blindaje RBAC**:
  - Se oculta de forma incondicional si `authStore.isStudent` es verdadero.

---

## 4. Requerimientos de Backend (`RTecNM_V2_Backend`)

### 4.1 Módulo de Empresas (`Companies`)
1. **Endpoint de Exportación**:
   - Crear endpoint `GET /api/v1/companies/export` en `CompaniesController.cs`.
   - Soporte para parámetros de consulta: `search`, `includeInactive`, `sector`, `sortBy`, `sortDir`.
   - Lista estricta de autorización para personal:
     `[Authorize(Roles = "admin,vinculacion,departmenthead,academic,director,jefecarrera,careerhead,coordinadora,coordinator")]`
2. **Servicio y Generación PDF**:
   - Implementar `ExportPdfAsync(string? search, string? sector, string? sortBy, string? sortDir, bool includeInactive)` en `ICompanyService` y `CompanyService.cs`.
   - Reutilizar el motor institucional `PdfExportService.GenerateTablePdf` con `QuestPDF`:
     - Título: `"Directorio Institucional de Empresas y Dependencias Receptoras"`.
     - Columnas: `"Nombre / Razón Social"`, `"RFC"`, `"Sector"`, `"Contacto"`, `"Correo"`, `"Teléfono"`, `"Convenio"`, `"Estado"`.
     - Orientación: A4 Landscape.

### 4.2 Módulo de Reportes de Liberación (`Admin/Reports`)
Para garantizar que `ReportsView.vue` funcione de forma íntegra sin romper ciclos ni hacer consultas a ciegas:
1. **Soporte de Búsqueda y Ordenamiento en `ReportGeneratorService.cs`**:
   - Actualizar `GetReleasableProjectsAsync` para que consuma `query.Search`, `query.SortBy` y `query.SortDir`:
     - Filtrar en memoria por coincidencia parcial en `Title`, `StudentName`, `ControlNumber` y `AdvisorName`.
     - Ordenar la lista resultante por la columna solicitada antes de aplicar `.Skip().Take()`.
2. **Endpoint de Exportación de Liberables**:
   - Agregar en `AdminController.cs`:
     `[HttpGet("reports/releasable/export")]`
     `[Authorize(Roles = "admin,departmenthead,director,coordinadora,coordinator")]`
   - Implementar en `ReportGeneratorService.cs` la generación del reporte en Excel o PDF institucional.

---

## 5. Plan de Ejecución Paso a Paso

### Fase 1: Primitivas y Blindaje de Autenticación
- [ ] **Paso 1.1**: Extender `RTecNM_V2_Frontend/src/stores/auth.js` agregando los computeds `isStudent` e `isStaff`.
- [ ] **Paso 1.2**: Crear el composable `RTecNM_V2_Frontend/src/composables/useTableSort.js`.
- [ ] **Paso 1.3**: Crear el componente `RTecNM_V2_Frontend/src/components/common/TecnmTableToolbar.vue`.
- [ ] **Paso 1.4**: Crear el componente `RTecNM_V2_Frontend/src/components/common/TecnmExportButton.vue`.
- [ ] **Paso 1.5**: Ajustar en `RTecNM_V2_Frontend/src/assets/css/main.css` las clases de soporte (`.tecnm-card-toolbar`, `.tecnm-search-box`, `.tecnm-th-sortable`) asegurando alineación vertical de `2.375rem` en controles.

### Fase 2: Backend (Exportación y Querying)
- [ ] **Paso 2.1**: Implementar `ExportPdfAsync` en `ICompanyService` y `CompanyService.cs` utilizando `PdfExportService`.
- [ ] **Paso 2.2**: Agregar endpoint `[HttpGet("export")]` en `CompaniesController.cs` con el rolset completo de personal.
- [ ] **Paso 2.3**: Actualizar `ReportGeneratorService.cs` para procesar `Search`, `SortBy` y `SortDir` en `GetReleasableProjectsAsync`, e implementar `ExportReleasableProjectsAsync`.
- [ ] **Paso 2.4**: Agregar endpoint `[HttpGet("reports/releasable/export")]` en `AdminController.cs`.
- [ ] **Paso 2.5**: Registrar pruebas unitarias en `RTecNM_V2_Backend.Tests` validando los nuevos endpoints y el filtrado.

### Fase 3: Homologación en Vistas de Personal (Staff)
- [ ] **Paso 3.1**: Actualizar `StudentsView.vue` adoptando `TecnmTableToolbar`, `useTableSort` y `TecnmExportButton`, removiendo el temporizador de búsqueda manual duplicado.
- [ ] **Paso 3.2**: Actualizar `AdvisorsView.vue` adoptando la toolbar estándar y el composable de ordenamiento.
- [ ] **Paso 3.3**: Actualizar `CompaniesView.vue` migrando de búsqueda con botón manual a búsqueda reactiva con *debounce* en la toolbar y conectando la exportación PDF.
- [ ] **Paso 3.4**: Actualizar `ReviewView.vue` homologando el ancho de búsqueda y adoptando `TecnmTableToolbar`.
- [ ] **Paso 3.5**: Actualizar `DocumentsView.vue` en la matriz de personal (`viewMode === 'matrix'`), unificando la barra de búsqueda y exportación.
- [ ] **Paso 3.6**: Actualizar `ReportsView.vue` conectando la búsqueda textual reactiva y el botón de exportación al backend habilitado en la Fase 2.
- [ ] **Paso 3.7**: Actualizar `RolesView.vue` (tablas de roles y usuarios administrativos).
- [ ] **Paso 3.8**: Actualizar `CareersView.vue` y `BroadcastNotificationsView.vue` homologando la iconografía de ordenamiento (`↑`, `↓`, `↕`) y el debounce de búsqueda.

### Fase 4: Blindaje y Verificación de Experiencia de Alumnos (`student`)
- [ ] **Paso 4.1**: Verificar que en `DocumentsView.vue` el estudiante solo vea su expediente personal y sus alertas de prórroga, con la matriz general y exportaciones masivas completamente ocultas.
- [ ] **Paso 4.2**: Verificar que en `GradingView.vue` y `AdvisorySessionsView.vue` el estudiante mantenga acceso exclusivo a sus calificaciones y bitácoras propias, sin toolbars administrativas.
- [ ] **Paso 4.3**: Confirmar que los botones de acción individual del alumno (descargar Solicitud PDF, Carta de Aceptación y reportes de avance) permanezcan intactos.

### Fase 5: Validación Técnica y Recibos RDD
- [ ] **Paso 5.1**: Ejecutar suite de pruebas de backend:  
  `dotnet test RTecNM_V2_Backend.Tests/RTecNM_V2_Backend.Tests.csproj`  
  (Garantizar 100% de pruebas superadas sin regresiones).
- [ ] **Paso 5.2**: Ejecutar compilación de frontend:  
  `pnpm --dir RTecNM_V2_Frontend build`  
  (0 errores de tipado, sintaxis o bundles huérfanos).
- [ ] **Paso 5.3**: Pruebas de integración visual con cuentas testigo:
  - `admin`: Acceso completo a toolbars, filtros y exportaciones masivas.
  - `coordinadora` / `jefecarrera`: Acceso a sus carreras correspondientes, filtros y exportaciones institucionales permitidas.
  - `student`: Visualización limpia sin herramientas administrativas, sin toolbars masivas y sin errores en consola.

---

## 6. Criterios de Aceptación y Garantía de Inocuidad

1. **Ausencia de Race Conditions**:
   - Escribir en cualquier barra de búsqueda dispara exactamente una petición HTTP tras 350 ms de inactividad.
   - El uso de la búsqueda modal global (`GlobalSearchModal`) actualiza inmediatamente el término en la barra de la tabla sin disparar consultas duplicadas ni desfasar la página.
2. **Paginación Consistente**:
   - Cada clic en una cabecera ordenable (`<th>`) reinicia de forma predecible `pageNumber = 1` y actualiza la vista con el indicador de dirección correspondiente.
3. **Seguridad RBAC Estricta**:
   - Ninguna llamada ni elemento de exportación masiva se expone a usuarios con rol `student`.
4. **Calidad de Compilación**:
   - Backend compila en .NET con 0 advertencias críticas y pasa las pruebas unitarias.
   - Frontend compila con Vite sin advertencias de dependencias ni fallas de bundle.
