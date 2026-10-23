# RTecNM_V2_Frontend.Tests

Suite oficial de pruebas unitarias y de componentes para el cliente Frontend (**Vue 3 + Vite + Pinia**), análoga a la suite `RTecNM_V2_Backend.Tests`.

## 📁 Estructura

```text
RTecNM_V2_Frontend.Tests/
├── package.json               # Configuración de dependencias de testing (Vitest, Vue Test Utils, Happy-DOM)
├── vitest.config.js           # Configuración de entorno y alias (@ -> ../RTecNM_V2_Frontend/src)
├── components/                # Pruebas de componentes Vue
│   ├── AuditModal.test.js     # Modal de auditoría de 10 campos
│   ├── ConfirmModal.test.js   # Modal de confirmación asíncrono
│   ├── TecnmBadge.test.js     # Badges semánticos por estado
│   ├── TecnmKpiCard.test.js   # Tarjetas de indicadores/KPIs
│   ├── TecnmModal.test.js     # Primitiva modal con Teleport
│   └── TecnmPagination.test.js# Paginación institucional
├── composables/               # Pruebas de composables reactivos
│   ├── useAudit.test.js       # Formateo de fechas, usuario semilla y filas
│   ├── useConfirm.test.js     # Flujo asíncrono de confirmación con Promises
│   └── useTableSort.test.js   # Ordenamiento ascendente/descendente y reactividad
└── stores/                    # Pruebas de stores Pinia
    └── auth.test.js           # RBAC, sesión, claims, roles y permisos
```

## 🚀 Comandos de Ejecución

Desde la carpeta del proyecto de pruebas (`RTecNM_V2_Frontend.Tests`):

```bash
# Ejecutar todas las pruebas una vez
pnpm test

# Modo interactivo / observador (Watch Mode)
pnpm test:watch

# Cobertura de código (Coverage)
pnpm test:coverage
```

O directamente desde `RTecNM_V2_Frontend`:

```bash
pnpm test
```
