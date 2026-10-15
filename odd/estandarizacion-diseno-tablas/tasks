# Estandarización del Diseño de Tablas, Filtros, Búsqueda y Exportación
Sistema de Gestión de Residencias Profesionales TecNM v2
Seguimiento de tareas y aseguramiento de ciclos de vida.

## Specs
- S1: "bueno entonces empieza a implmentar el plan al proyecto, teneindo cuidado de dañar los procesos y ciclos del proyecto en este punto, tamdno como prioridad, la eficiencia, coerencia, ciclos, entre otros puntos para que no se dañe el sistema, estos puntos son mas importantes que la velocidad de las modifcaciones que realizes"
- S2: Implementar getters `isStudent` e `isStaff` en `RTecNM_V2_Frontend/src/stores/auth.js`.
- S3: Implementar composable `useTableSort.js` con ordenamiento seguro, soporte Server-side y Client-side, y reinicio de página a 1 en consultas paginadas.
- S4: Implementar componente `TecnmTableToolbar.vue` con debounce de 350ms, soporte de `v-model` transparente y slots `#filters` y `#actions`.
- S5: Implementar componente `TecnmExportButton.vue` con ocultamiento automático para estudiantes y soporte para formatos PDF y Excel.
- S6: Ajustar clases institucionales en `main.css` para toolbars y alineación vertical de controles (`2.375rem`).
- S7: Backend Empresas: `ExportPdfAsync` en `ICompanyService` y endpoint `GET /api/v1/companies/export` en `CompaniesController.cs` con rolset completo de staff.
- S8: Backend Reportes: Soporte de `Search`, `SortBy`, `SortDir` en `ReportGeneratorService.cs` y endpoint `GET /api/v1/admin/reports/releasable/export` en `AdminController.cs`.
- S9: Registrar y ejecutar pruebas unitarias en `RTecNM_V2_Backend.Tests` (`dotnet test`).
- S10: Actualizar vistas de personal: `StudentsView`, `AdvisorsView`, `CompaniesView`, `ReviewView`, `DocumentsView`, `ReportsView`, `RolesView`, `CareersView`, `BroadcastNotificationsView`.
- S11: Blindaje estricto de vistas y accesos para rol estudiante (`student`).
- S12: Verificación técnica completa de compilación (`pnpm build`) y ejecución de pruebas backend con 0 regresiones.

## Tasks
- T1 (S2) - [x] Agregar getters `isStudent` e `isStaff` en `RTecNM_V2_Frontend/src/stores/auth.js` | inline
- T2 (S3) - [x] Crear composable `RTecNM_V2_Frontend/src/composables/useTableSort.js` | inline
- T3 (S4) - [x] Crear componente `RTecNM_V2_Frontend/src/components/common/TecnmTableToolbar.vue` | inline
- T4 (S5) - [x] Crear componente `RTecNM_V2_Frontend/src/components/common/TecnmExportButton.vue` | inline
- T5 (S6) - [x] Homologar clases de toolbar y diseño en `RTecNM_V2_Frontend/src/assets/css/main.css` | inline
- T6 (S7) - [x] Implementar `ExportPdfAsync` y endpoint `GET /api/v1/companies/export` en `CompaniesController.cs` | inline
- T7 (S8) - [x] Implementar `Search`, `SortBy`, `SortDir` y exportación de liberables en `Admin/ReportGeneratorService.cs` y `AdminController.cs` | inline
- T8 (S9) - [x] Agregar pruebas unitarias en `RTecNM_V2_Backend.Tests` y verificar suite `dotnet test` | inline
- T9 (S10) - [x] Homologar `StudentsView.vue` y `AdvisorsView.vue` con toolbar y sorting estándar | inline
- T10 (S10) - [x] Homologar `CompaniesView.vue` y `ReviewView.vue` con toolbar y debounce reactivo | inline
- T11 (S10) - [x] Homologar `DocumentsView.vue` (matriz de expedientes) con toolbar y exportación | inline
- T12 (S10) - [x] Homologar `ReportsView.vue` conectando búsqueda y exportación tabular al backend | inline
- T13 (S10) - [x] Homologar `RolesView.vue`, `CareersView.vue` y `BroadcastNotificationsView.vue` | inline
- T14 (S11) - [x] Verificar blindaje de rol estudiante en `DocumentsView.vue`, `GradingView.vue` y `AdvisorySessionsView.vue` | inline
- T15 (S12) - [x] Ejecutar compilación de frontend (`pnpm build`) y suite de pruebas backend (`dotnet test`) | inline

## Log
- L1: "bueno entonces empieza a implmentar el plan al proyecto, teneindo cuidado de dañar los procesos y ciclos del proyecto en este punto, tamdno como prioridad, la eficiencia, coerencia, ciclos, entre otros puntos para que no se dañe el sistema, estos puntos son mas importantes que la velocidad de las modifcaciones que realizes"
- L2: Implementación finalizada y verificada con 34/34 pruebas en backend (.NET 10) y build sin errores en frontend (Vite).
