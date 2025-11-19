# **Guía: Background Jobs en .NET**

## **¿Qué son los Background Jobs?**

Los Background Jobs (trabajos en segundo plano) son tareas que se ejecutan de forma asíncrona sin bloquear la respuesta HTTP al usuario. Son ideales para:

- Procesamiento de imágenes con IA
- Envío de emails
- Generación de reportes
- Sincronización de datos
- Tareas programadas (cron jobs)

---

## **Opciones en .NET**

### **1. Hangfire** ⭐ (Recomendado para este proyecto)

#### **Ventajas:**
- ✅ **Muy fácil de configurar** - Solo agregar paquete NuGet
- ✅ **Dashboard web integrado** - Interfaz visual para monitorear jobs
- ✅ **Persistencia en base de datos** - Los jobs sobreviven reinicios del servidor
- ✅ **Reintentos automáticos** - Configuración de reintentos en caso de fallo
- ✅ **Soporte para colas** - Múltiples colas (alta prioridad, baja prioridad)
- ✅ **Programación flexible** - Cron expressions para tareas recurrentes
- ✅ **Escalable** - Múltiples servidores pueden procesar la misma cola

#### **Desventajas:**
- ⚠️ Requiere base de datos adicional (SQL Server, PostgreSQL, etc.)
- ⚠️ Dashboard consume recursos (aunque mínimo)

#### **Caso de uso ideal:**
```csharp
// Ejemplo: Análisis de imagen en segundo plano
BackgroundJob.Enqueue<IAiSstAnalyzer>(x => x.AnalyzeImageAsync(imageId));

// Ejemplo: Análisis programado para todas las imágenes pendientes
RecurringJob.AddOrUpdate("analyze-pending", 
    () => _analysisService.ProcessPendingImages(), 
    Cron.Hourly);
```

#### **Instalación:**
```bash
dotnet add package Hangfire.Core
dotnet add package Hangfire.AspNetCore
dotnet add package Hangfire.SqlServer  # Para SQL Server
```

---

### **2. Quartz.NET**

#### **Ventajas:**
- ✅ **Muy potente y flexible** - Sistema de scheduling muy robusto
- ✅ **Cron expressions avanzadas** - Control fino sobre cuándo ejecutar
- ✅ **Clustering** - Distribución de jobs entre múltiples servidores
- ✅ **Persistencia opcional** - Puede usar memoria o base de datos
- ✅ **Múltiples triggers** - Muy flexible para tareas complejas

#### **Desventajas:**
- ⚠️ **Más complejo de configurar** - Requiere más código inicial
- ⚠️ **Curva de aprendizaje** - Conceptos como Jobs, Triggers, Schedulers
- ⚠️ **Sin dashboard integrado** - Necesitas construir tu propia UI

#### **Caso de uso ideal:**
```csharp
// Ejemplo: Job programado con Quartz
var job = JobBuilder.Create<ImageAnalysisJob>()
    .WithIdentity("analyze-image", "analysis-group")
    .Build();

var trigger = TriggerBuilder.Create()
    .WithIdentity("analyze-trigger", "analysis-group")
    .StartNow()
    .Build();

await scheduler.ScheduleJob(job, trigger);
```

#### **Instalación:**
```bash
dotnet add package Quartz
dotnet add package Quartz.Serialization.Json
```

---

### **3. IHostedService (Built-in .NET)**

#### **Ventajas:**
- ✅ **Sin dependencias externas** - Ya viene con .NET
- ✅ **Ligero** - No consume recursos adicionales
- ✅ **Control total** - Implementas todo tú mismo

#### **Desventajas:**
- ⚠️ **Sin persistencia** - Si el servidor se reinicia, se pierden los jobs
- ⚠️ **Sin dashboard** - No hay interfaz visual
- ⚠️ **Sin reintentos automáticos** - Debes implementarlos manualmente
- ⚠️ **No escalable fácilmente** - Más difícil distribuir entre servidores

#### **Caso de uso ideal:**
Tareas simples que no requieren persistencia ni monitoreo avanzado.

---

## **Recomendación para VisioAnalytica**

### **Hangfire es la mejor opción porque:**

1. **Análisis de imágenes en segundo plano:**
   - Las imágenes pueden tardar varios segundos en analizarse
   - No queremos que el usuario espere
   - Hangfire maneja esto perfectamente

2. **Dashboard de monitoreo:**
   - Podrás ver qué imágenes están siendo procesadas
   - Ver cuántas fallaron y por qué
   - Reintentar manualmente si es necesario

3. **Persistencia:**
   - Si el servidor se reinicia, los análisis pendientes continúan
   - No se pierden trabajos

4. **Escalabilidad futura:**
   - Cuando crezca el negocio, puedes agregar más servidores
   - Todos procesan la misma cola

5. **Facilidad de implementación:**
   - Configuración rápida
   - Integración sencilla con tu código existente

---

## **Ejemplo de Implementación con Hangfire**

### **1. Configuración en Program.cs:**

```csharp
// Agregar Hangfire
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString));

builder.Services.AddHangfireServer();

// Dashboard (solo en desarrollo o para admins)
if (app.Environment.IsDevelopment())
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new HangfireAuthorizationFilter() }
    });
}
```

### **2. Uso en tu servicio:**

```csharp
public class AnalysisService
{
    private readonly IBackgroundJobClient _backgroundJob;
    
    public AnalysisService(IBackgroundJobClient backgroundJob)
    {
        _backgroundJob = backgroundJob;
    }
    
    public void QueueImageAnalysis(Guid photoId)
    {
        // Encolar análisis en segundo plano
        _backgroundJob.Enqueue<IAiSstAnalyzer>(
            analyzer => analyzer.AnalyzeImageAsync(photoId));
    }
}
```

### **3. Notificación al completar:**

```csharp
public void QueueImageAnalysis(Guid photoId, Guid userId)
{
    _backgroundJob.Enqueue<IAiSstAnalyzer>(
        analyzer => analyzer.AnalyzeImageAsync(photoId));
    
    // Continuar con notificación cuando termine
    _backgroundJob.ContinueJobWith<INotificationService>(
        jobId,
        service => service.NotifyAnalysisComplete(userId, photoId));
}
```

---

## **Comparación Rápida**

| Característica | Hangfire | Quartz.NET | IHostedService |
|---------------|----------|------------|---------------|
| Facilidad de uso | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐ |
| Dashboard | ✅ Sí | ❌ No | ❌ No |
| Persistencia | ✅ Sí | ✅ Opcional | ❌ No |
| Reintentos | ✅ Sí | ✅ Sí | ❌ Manual |
| Escalabilidad | ✅ Excelente | ✅ Excelente | ⚠️ Limitada |
| Curva aprendizaje | ⭐ Fácil | ⭐⭐ Media | ⭐⭐⭐ Media |

---

## **Decisión Final**

**Recomendación: Hangfire**

- Perfecto para análisis de imágenes asíncronos
- Dashboard útil para monitoreo
- Fácil de implementar y mantener
- Escalable para el futuro

¿Procedemos con Hangfire?

