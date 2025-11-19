# **Plan de Trabajo Priorizado - VisioAnalytica**

## **Estado Actual (Capítulo 6 - Completado)**

### **✅ Frontend MAUI Implementado:**
- LoginPage, RegisterPage, CapturePage, ResultsPage, HistoryPage, MainPage
- Servicios: ApiClient, AuthService, AnalysisService
- Navegación básica funcionando
- Captura de una foto y análisis simple

### **⚠️ Limitaciones Actuales:**
- Solo una foto por inspección
- No hay selección de empresa afiliada
- No hay roles (todos los usuarios son iguales)
- No hay recuperación de contraseña
- HistoryPage no está conectada a la API
- No hay modo offline

---

## **Orden de Implementación**

### **ETAPA 1: Backend - Fundamentos (Fase 1)** 🔧
**Objetivo:** Crear la base sólida del sistema antes de modificar el frontend

#### **1.1 Sistema de Roles y Permisos** (Backend)
- [ ] Crear roles: SuperAdmin, Admin, Inspector, Cliente
- [ ] Actualizar modelo `User` con propiedades necesarias
- [ ] Crear entidad `AffiliatedCompany` (Empresas Afiliadas)
- [ ] Crear relación Many-to-Many: Inspector ↔ Empresas Afiliadas
- [ ] Endpoints para gestión de roles (solo SuperAdmin/Admin)
- [ ] Actualizar `TokenService` para incluir roles en JWT
- [ ] Migración de base de datos

**Branch:** `feature/roles-y-permisos`  
**Tiempo estimado:** 2-3 días  
**Dependencias:** Ninguna

---

#### **1.2 Recuperación de Contraseña** (Backend)
- [ ] Crear interfaz `IEmailService`
- [ ] Implementar `SmtpEmailService` (desarrollo)
- [ ] Endpoint `POST /api/auth/forgot-password`
- [ ] Endpoint `POST /api/auth/reset-password`
- [ ] Endpoint `POST /api/auth/change-password` (cambio obligatorio)
- [ ] Plantillas de email HTML

**Branch:** `feature/password-recovery`  
**Tiempo estimado:** 1-2 días  
**Dependencias:** 1.1 (necesita roles para validar permisos)

---

#### **1.3 Seguridad de Imágenes Mejorada** (Backend)
- [ ] Mover imágenes fuera de `wwwroot`
- [ ] Mejorar `FileController` con validación de roles
- [ ] Políticas de acceso por empresa afiliada
- [ ] Endpoint para eliminar imágenes

**Branch:** `feature/image-security`  
**Tiempo estimado:** 1 día  
**Dependencias:** 1.1 (necesita roles)

---

#### **1.4 Gestión de Empresas Afiliadas** (Backend)
- [ ] Endpoints CRUD para `AffiliatedCompany` (solo Admin)
- [ ] Endpoint para asignar inspectores a empresas
- [ ] Endpoint para listar empresas asignadas a un inspector
- [ ] Validaciones de negocio

**Branch:** `feature/affiliated-companies`  
**Tiempo estimado:** 1-2 días  
**Dependencias:** 1.1

---

### **ETAPA 2: Adaptar Frontend MAUI a Nuevo Backend** 📱
**Objetivo:** Hacer que el frontend existente funcione con los nuevos endpoints

#### **2.1 Actualizar Autenticación en MAUI**
- [ ] Actualizar `AuthService` para manejar cambio de contraseña obligatorio
- [ ] Agregar página `ForgotPasswordPage`
- [ ] Agregar página `ChangePasswordPage` (primera vez)
- [ ] Actualizar `LoginPage` para detectar cambio de contraseña requerido
- [ ] Actualizar modelos para incluir roles

**Branch:** `feature/maui-auth-update`  
**Tiempo estimado:** 1 día  
**Dependencias:** 1.1, 1.2

---

#### **2.2 Selección de Empresa Afiliada**
- [ ] Crear página `SelectCompanyPage`
- [ ] Servicio `ICompanyService` en MAUI
- [ ] Mostrar lista de empresas asignadas al inspector
- [ ] Guardar empresa seleccionada en sesión
- [ ] Integrar en flujo de inspección

**Branch:** `feature/maui-company-selection`  
**Tiempo estimado:** 1 día  
**Dependencias:** 1.4, 2.1

---

#### **2.3 Conectar HistoryPage a API**
- [ ] Endpoint `GET /api/inspections/history` (si no existe)
- [ ] Actualizar `HistoryPage` para cargar datos reales
- [ ] Mostrar lista de inspecciones con detalles
- [ ] Navegación a detalles de inspección

**Branch:** `feature/maui-history-integration`  
**Tiempo estimado:** 1 día  
**Dependencias:** 1.1

---

### **ETAPA 3: Mejorar Frontend MAUI - Nuevas Funcionalidades** 🚀
**Objetivo:** Implementar las funcionalidades avanzadas solicitadas

#### **3.1 Múltiples Fotos por Inspección** (Backend + Frontend)
- [ ] Actualizar modelo `Inspection` y crear `InspectionPhoto`
- [ ] Endpoint para crear inspección con múltiples fotos
- [ ] Actualizar `CapturePage` para tomar múltiples fotos
- [ ] Crear `PhotoGalleryPage` con miniaturas
- [ ] Sistema de selección de fotos (checkboxes)
- [ ] Envío de fotos seleccionadas para análisis

**Branch:** `feature/multiple-photos`  
**Tiempo estimado:** 3-4 días  
**Dependencias:** 1.1, 2.2

---

#### **3.2 Cámara Avanzada**
- [ ] Crear `AdvancedCameraPage` con controles:
  - Zoom
  - Recorte
  - Filtros básicos
  - Flash
  - Ajuste de calidad/brillo
- [ ] Integrar en flujo de captura

**Branch:** `feature/advanced-camera`  
**Tiempo estimado:** 2-3 días  
**Dependencias:** 3.1

---

#### **3.3 Galería de Fotos Interactiva**
- [ ] Mejorar `PhotoGalleryPage`:
  - Tap para ampliar
  - Zoom en imagen ampliada
  - Eliminar foto de lista
  - Reordenar fotos
- [ ] Mejorar UX con animaciones

**Branch:** `feature/photo-gallery`  
**Tiempo estimado:** 2 días  
**Dependencias:** 3.1

---

#### **3.4 Análisis en Segundo Plano**
- [ ] Instalar y configurar Hangfire
- [ ] Crear job para análisis de imágenes
- [ ] Endpoint para consultar estado de análisis
- [ ] Notificaciones push cuando termine
- [ ] Actualizar UI para mostrar progreso

**Branch:** `feature/background-analysis`  
**Tiempo estimado:** 2-3 días  
**Dependencias:** 3.1

---

#### **3.5 Notificaciones**
- [ ] Implementar `INotificationService` (email)
- [ ] Notificaciones push en MAUI
- [ ] Notificación cuando análisis complete
- [ ] Configuración de preferencias de notificación

**Branch:** `feature/notifications`  
**Tiempo estimado:** 2 días  
**Dependencias:** 3.4, 1.2

---

### **ETAPA 4: Modo Offline** 📴
**Objetivo:** Funcionalidad completa sin conexión

#### **4.1 SQLite Local**
- [ ] Crear `LocalDbContext` en MAUI
- [ ] Entidades locales: `LocalInspection`, `LocalPhoto`
- [ ] Servicio `ILocalStorageService`
- [ ] Guardar fotos localmente

**Branch:** `feature/offline-storage`  
**Tiempo estimado:** 2-3 días  
**Dependencias:** 3.1

---

#### **4.2 Sincronización**
- [ ] Servicio `ISyncService`
- [ ] Detectar conexión
- [ ] Subir fotos pendientes
- [ ] Sincronizar estado
- [ ] Resolución de conflictos
- [ ] Indicador de estado de sync

**Branch:** `feature/offline-sync`  
**Tiempo estimado:** 3-4 días  
**Dependencias:** 4.1

---

### **ETAPA 5: Ideas de Valor Agregado** 💎
**Objetivo:** Funcionalidades que diferencian el producto

#### **5.1 Sistema de Scoring** (Alto Impacto, Baja Complejidad)
- [ ] Agregar `RiskScore` y `Priority` a `Finding`
- [ ] Calcular scores automáticamente
- [ ] Mostrar en UI con colores/iconos
- [ ] Filtrar por prioridad

**Branch:** `feature/risk-scoring`  
**Tiempo estimado:** 1-2 días  
**Dependencias:** 3.1

---

#### **5.2 Exportación de Reportes**
- [ ] Endpoint para generar PDF
- [ ] Endpoint para generar Excel
- [ ] Plantillas personalizables
- [ ] Botón de exportar en ResultsPage

**Branch:** `feature/report-export`  
**Tiempo estimado:** 2-3 días  
**Dependencias:** 3.1

---

#### **5.3 Dashboard Analítico** (Backend + Frontend)
- [ ] Endpoints de métricas y KPIs
- [ ] Gráficos en backend (o frontend)
- [ ] Dashboard en MAUI o Web Admin
- [ ] Filtros y comparaciones

**Branch:** `feature/analytics-dashboard`  
**Tiempo estimado:** 3-4 días  
**Dependencias:** 3.1

---

#### **5.4 Comparación Temporal**
- [ ] Endpoint para comparar inspecciones
- [ ] Gráficos de evolución
- [ ] UI para comparar fechas
- [ ] Alertas de deterioro

**Branch:** `feature/temporal-comparison`  
**Tiempo estimado:** 2-3 días  
**Dependencias:** 5.3

---

## **Cronograma Sugerido**

### **Sprint 1 (Semana 1-2): Backend Fundamentos**
- 1.1 Sistema de Roles ✅
- 1.2 Recuperación de Contraseña ✅
- 1.3 Seguridad de Imágenes ✅
- 1.4 Empresas Afiliadas ✅

### **Sprint 2 (Semana 3): Adaptar Frontend**
- 2.1 Actualizar Autenticación ✅
- 2.2 Selección de Empresa ✅
- 2.3 Conectar HistoryPage ✅

### **Sprint 3-4 (Semana 4-6): Funcionalidades Core**
- 3.1 Múltiples Fotos ✅
- 3.2 Cámara Avanzada ✅
- 3.3 Galería Interactiva ✅

### **Sprint 5 (Semana 7): Análisis y Notificaciones**
- 3.4 Análisis en Segundo Plano ✅
- 3.5 Notificaciones ✅

### **Sprint 6-7 (Semana 8-10): Modo Offline**
- 4.1 SQLite Local ✅
- 4.2 Sincronización ✅

### **Sprint 8+ (Semana 11+): Valor Agregado**
- 5.1 Scoring ✅
- 5.2 Exportación ✅
- 5.3 Dashboard ✅
- 5.4 Comparación Temporal ✅

---

## **Reglas de Trabajo**

1. **No romper lo que funciona:**
   - Cada cambio debe mantener compatibilidad con lo existente
   - Testear antes de merge

2. **Backend primero, Frontend después:**
   - Implementar endpoints antes de consumirlos
   - Frontend se adapta al backend, no al revés

3. **Una feature a la vez:**
   - Un branch, una funcionalidad
   - Merge a develop cuando esté completa y testeada

4. **Documentar cambios importantes:**
   - Actualizar docs cuando cambie arquitectura
   - Comentar decisiones no obvias

---

## **Próximo Paso Inmediato**

**Crear branch `feature/roles-y-permisos` y comenzar con 1.1**

¿Procedemos con la Etapa 1.1?

