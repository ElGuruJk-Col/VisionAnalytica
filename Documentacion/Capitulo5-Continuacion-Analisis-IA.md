# **Capítulo 5: Continuación - Análisis de IA y Persistencia Completa**

Este documento continúa el desarrollo del Capítulo 5, completando las funcionalidades pendientes del sistema de análisis de IA y persistencia de datos.

## **Estado Actual del Capítulo 5**

### **✅ Completado:**
- ✅ GeminiAnalyzer implementado y funcional (v4.2)
- ✅ AnalysisService orquestador implementado
- ✅ AnalysisController con endpoints de análisis
- ✅ Modelos de persistencia (Inspection, Finding)
- ✅ Repository pattern implementado (AnalysisRepository)
- ✅ ReportService para consultas históricas
- ✅ Endpoints de lectura (historial y detalles)

### **⏳ Pendiente:**
- ⏳ Almacenamiento de imágenes (Blob Storage o alternativa local)
- ⏳ Configuración completa de prompts en appsettings.json
- ⏳ Mejoras en ReportService (incluir UserName correctamente)
- ⏳ Validación y pruebas end-to-end

---

## **PASO 28 (de 35): Crear el Contrato de Almacenamiento de Archivos (`IFileStorage`)**

* **EL QUÉ (La Tarea):**
    Creamos una nueva interfaz `src/Core/Interfaces/IFileStorage.cs` que define el contrato para almacenar y recuperar imágenes.

* **EL PARA QUÉ (El "Porqué"):**
    Para **Abstraer** el almacenamiento de archivos siguiendo Arquitectura Limpia.
    * **La "Teoría Sólida":** Este contrato permite cambiar entre diferentes proveedores de almacenamiento (Azure Blob Storage, AWS S3, almacenamiento local) sin modificar el código de negocio.
    * Actualmente, el `AnalysisService` tiene un `TODO` en la línea 108 que dice `ImageUrl = "temp/image_b64_not_uploaded.jpg"`. Necesitamos reemplazar esto con una implementación real.

* **EL CÓMO (El Código):**
    ```csharp
    // En: src/Core/Interfaces/IFileStorage.cs
    
    namespace VisioAnalytica.Core.Interfaces
    {
        /// <summary>
        /// Contrato para el almacenamiento de archivos (imágenes).
        /// Permite abstraer el proveedor de almacenamiento (Azure Blob, Local, etc.)
        /// </summary>
        public interface IFileStorage
        {
            /// <summary>
            /// Guarda una imagen y devuelve su URL o ruta de acceso.
            /// </summary>
            /// <param name="imageBytes">Los bytes de la imagen</param>
            /// <param name="fileName">Nombre sugerido del archivo (opcional)</param>
            /// <param name="organizationId">ID de la organización (para multi-tenancy)</param>
            /// <returns>La URL o ruta donde se guardó la imagen</returns>
            Task<string> SaveImageAsync(byte[] imageBytes, string? fileName = null, Guid? organizationId = null);
            
            /// <summary>
            /// Elimina una imagen del almacenamiento.
            /// </summary>
            /// <param name="imageUrl">La URL o ruta de la imagen a eliminar</param>
            /// <returns>True si se eliminó correctamente, False en caso contrario</returns>
            Task<bool> DeleteImageAsync(string imageUrl);
        }
    }
    ```

---

## **PASO 29 (de 35): Implementar Almacenamiento Local (Fase de Desarrollo)**

* **EL QUÉ (La Tarea):**
    Creamos una implementación de `IFileStorage` que guarda las imágenes en el sistema de archivos local (carpeta `wwwroot/uploads`).

* **EL PARA QUÉ (El "Porqué"):**
    Para tener una solución funcional **inmediata** sin depender de servicios externos (Azure Blob Storage).
    * **La "Teoría Sólida":** Esta implementación es perfecta para desarrollo y demos. En producción, podemos crear una implementación `AzureBlobStorage` que implemente el mismo contrato.
    * Usaremos una estructura de carpetas organizada por organización: `wwwroot/uploads/{organizationId}/{timestamp}_{filename}`

* **EL CÓMO (El Código):**
    ```csharp
    // En: src/Infrastructure/Services/LocalFileStorage.cs
    
    using Microsoft.Extensions.Logging;
    using VisioAnalytica.Core.Interfaces;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    
    namespace VisioAnalytica.Infrastructure.Services
    {
        /// <summary>
        /// Implementación de IFileStorage usando el sistema de archivos local.
        /// Ideal para desarrollo y demos. En producción, usar AzureBlobStorage.
        /// </summary>
        public class LocalFileStorage : IFileStorage
        {
            private readonly string _uploadsPath;
            private readonly ILogger<LocalFileStorage> _logger;
            private readonly IWebHostEnvironment _environment;
    
            public LocalFileStorage(IWebHostEnvironment environment, ILogger<LocalFileStorage> logger)
            {
                _environment = environment ?? throw new ArgumentNullException(nameof(environment));
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                
                // Definimos la ruta base: wwwroot/uploads
                _uploadsPath = Path.Combine(_environment.WebRootPath ?? _environment.ContentRootPath, "uploads");
                
                // Aseguramos que la carpeta exista
                if (!Directory.Exists(_uploadsPath))
                {
                    Directory.CreateDirectory(_uploadsPath);
                    _logger.LogInformation("Carpeta de uploads creada en: {UploadsPath}", _uploadsPath);
                }
            }
    
            public async Task<string> SaveImageAsync(byte[] imageBytes, string? fileName = null, Guid? organizationId = null)
            {
                try
                {
                    // 1. Generar nombre único si no se proporciona
                    if (string.IsNullOrWhiteSpace(fileName))
                    {
                        var hash = ComputeHash(imageBytes);
                        fileName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{hash[..8]}.jpg";
                    }
                    else
                    {
                        // Sanitizar el nombre del archivo
                        fileName = SanitizeFileName(fileName);
                    }
    
                    // 2. Construir la ruta completa
                    string finalPath;
                    if (organizationId.HasValue)
                    {
                        // Organizar por organización: uploads/{orgId}/{filename}
                        var orgFolder = Path.Combine(_uploadsPath, organizationId.Value.ToString());
                        if (!Directory.Exists(orgFolder))
                        {
                            Directory.CreateDirectory(orgFolder);
                        }
                        finalPath = Path.Combine(orgFolder, fileName);
                    }
                    else
                    {
                        finalPath = Path.Combine(_uploadsPath, fileName);
                    }
    
                    // 3. Guardar el archivo
                    await File.WriteAllBytesAsync(finalPath, imageBytes);
                    _logger.LogInformation("Imagen guardada en: {FilePath}", finalPath);
    
                    // 4. Devolver la URL relativa (para que el servidor web pueda servirla)
                    var relativePath = finalPath.Replace(_environment.WebRootPath ?? _environment.ContentRootPath, "")
                                                .Replace("\\", "/")
                                                .TrimStart('/');
                    
                    return $"/{relativePath}";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al guardar imagen. FileName: {FileName}, OrgId: {OrgId}", fileName, organizationId);
                    throw new InvalidOperationException("No se pudo guardar la imagen en el almacenamiento local.", ex);
                }
            }
    
            public async Task<bool> DeleteImageAsync(string imageUrl)
            {
                try
                {
                    // Convertir URL relativa a ruta física
                    var physicalPath = imageUrl.StartsWith("/")
                        ? Path.Combine(_environment.WebRootPath ?? _environment.ContentRootPath, imageUrl.TrimStart('/'))
                        : imageUrl;
    
                    // Reemplazar '/' por separador de directorio del sistema
                    physicalPath = physicalPath.Replace("/", Path.DirectorySeparatorChar);
    
                    if (File.Exists(physicalPath))
                    {
                        File.Delete(physicalPath);
                        _logger.LogInformation("Imagen eliminada: {FilePath}", physicalPath);
                        return await Task.FromResult(true);
                    }
    
                    _logger.LogWarning("Imagen no encontrada para eliminar: {ImageUrl}", imageUrl);
                    return await Task.FromResult(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al eliminar imagen: {ImageUrl}", imageUrl);
                    return false;
                }
            }
    
            private static string ComputeHash(byte[] data)
            {
                using var sha256 = SHA256.Create();
                var hashBytes = sha256.ComputeHash(data);
                return Convert.ToHexString(hashBytes).ToLowerInvariant();
            }
    
            private static string SanitizeFileName(string fileName)
            {
                // Eliminar caracteres peligrosos y espacios
                var invalidChars = Path.GetInvalidFileNameChars();
                var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
                return sanitized.Trim();
            }
        }
    }
    ```

---

## **PASO 30 (de 35): Integrar IFileStorage en AnalysisService**

* **EL QUÉ (La Tarea):**
    Modificamos `AnalysisService` para usar `IFileStorage` y guardar las imágenes antes de persistir la inspección.

* **EL PARA QUÉ (El "Porqué"):**
    Para completar el flujo de análisis: ahora las imágenes se guardan físicamente y su URL se almacena en la base de datos.

* **EL CÓMO (El Código):**
    ```csharp
    // En: src/Infrastructure/Services/AnalysisService.cs
    // MODIFICAR el constructor y el método PerformSstAnalysisAsync
    
    public class AnalysisService(
        IAiSstAnalyzer aiAnalyzer,
        IAnalysisRepository analysisRepository,
        IFileStorage fileStorage, // <-- ¡NUEVO!
        ILogger<AnalysisService> logger,
        IConfiguration configuration) : IAnalysisService
    {
        private readonly IFileStorage _fileStorage = fileStorage; // <-- ¡NUEVO!
        // ... resto de campos ...
    
        public async Task<SstAnalysisResult?> PerformSstAnalysisAsync(AnalysisRequestDto request, string userId, Guid organizationId)
        {
            // ... código existente hasta la línea 90 ...
    
            // 3. Guardar la imagen ANTES de persistir la inspección
            string imageUrl;
            try
            {
                var imageBytes = Convert.FromBase64String(request.ImageBase64);
                imageUrl = await _fileStorage.SaveImageAsync(imageBytes, null, organizationId);
                _logger.LogInformation("Imagen guardada en: {ImageUrl}", imageUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar la imagen para el usuario {UserId}.", userId);
                throw new InvalidOperationException("No se pudo guardar la imagen. El análisis se canceló.", ex);
            }
    
            // 3.A: Construir la Inspección (cabecera) - MODIFICADO
            // ... código existente ...
            var inspection = new Inspection
            {
                UserId = parsedUserId,
                OrganizationId = organizationId,
                ImageUrl = imageUrl, // <-- ¡REEMPLAZADO! Ya no es "temp/..."
            };
    
            // ... resto del código sin cambios ...
        }
    }
    ```

---

## **PASO 31 (de 35): Registrar IFileStorage en DependencyInjection**

* **EL QUÉ (La Tarea):**
    Añadimos el registro de `IFileStorage` en `DependencyInjectionExtensions.cs`.

* **EL PARA QUÉ (El "Porqué"):**
    Para que el contenedor de dependencias pueda inyectar `LocalFileStorage` cuando se solicite `IFileStorage`.

* **EL CÓMO (El Código):**
    ```csharp
    // En: src/Api/Extensions/DependencyInjectionExtensions.cs
    // Añadir después del registro de IAnalysisRepository (línea 83)
    
    // 4.G: ¡NUEVO REGISTRO! El Servicio de Almacenamiento de Archivos
    services.AddScoped<IFileStorage, LocalFileStorage>(); // << ¡AÑADIDO!
    ```

---

## **PASO 32 (de 35): Configurar el Prompt Maestro de SST**

* **EL QUÉ (La Tarea):**
    Completamos la configuración del prompt maestro en `appsettings.json`.

* **EL PARA QUÉ (El "Porqué"):**
    El `AnalysisService` requiere que el prompt esté configurado. Sin él, la aplicación lanzará una excepción al iniciar.

* **EL CÓMO (El Código):**
    ```json
    // En: src/Api/appsettings.json
    // Completar la sección "AiPrompts"
    
    "AiPrompts": {
      "MasterSst": "Eres un experto en Seguridad y Salud en el Trabajo (SST). Analiza la imagen proporcionada e identifica todos los riesgos, peligros y condiciones inseguras relacionadas con SST. Para cada hallazgo, proporciona:\n\n1. Descripcion: Una descripción clara y específica del hallazgo.\n2. NivelRiesgo: ALTO, MEDIO o BAJO.\n3. AccionCorrectiva: La acción inmediata que debe tomarse para corregir el riesgo.\n4. AccionPreventiva: La acción a largo plazo para prevenir que este riesgo vuelva a ocurrir (causa raíz).\n\nResponde ÚNICAMENTE con un JSON válido en este formato:\n{\n  \"Hallazgos\": [\n    {\n      \"Descripcion\": \"...\",\n      \"NivelRiesgo\": \"ALTO|MEDIO|BAJO\",\n      \"AccionCorrectiva\": \"...\",\n      \"AccionPreventiva\": \"...\"\n    }\n  ]\n}\n\nNo incluyas texto adicional fuera del JSON. Si no encuentras riesgos, devuelve un array vacío: {\"Hallazgos\": []}."
    }
    ```

---

## **PASO 33 (de 35): Mejorar ReportService para Incluir UserName**

* **EL QUÉ (La Tarea):**
    Modificamos `AnalysisRepository` y `ReportService` para incluir el `User` (con su `UserName`) en las consultas.

* **EL PARA QUÉ (El "Porqué"):**
    Actualmente, `ReportService.GetInspectionHistoryAsync` muestra un placeholder `"Usuario ID: {i.UserId.ToString()[..4]}..."`. Necesitamos mostrar el nombre real del usuario.

* **EL CÓMO (El Código):**
    ```csharp
    // En: src/Infrastructure/Data/AnalysisRepository.cs
    // MODIFICAR GetInspectionsByOrganizationAsync
    
    public async Task<IReadOnlyList<Inspection>> GetInspectionsByOrganizationAsync(Guid organizationId)
    {
        var inspections = await _context.Inspections
            .Include(i => i.Findings)
            .Include(i => i.User) // <-- ¡AÑADIDO! Para cargar el User con su UserName
            .Where(i => i.OrganizationId == organizationId)
            .OrderByDescending(i => i.AnalysisDate)
            .AsNoTracking()
            .ToListAsync();
    
        return inspections;
    }
    
    // También modificar GetInspectionByIdAsync para consistencia
    public async Task<Inspection?> GetInspectionByIdAsync(Guid inspectionId)
    {
        return await _context.Inspections
            .Include(i => i.Findings)
            .Include(i => i.User) // <-- ¡AÑADIDO!
            .FirstOrDefaultAsync(i => i.Id == inspectionId);
    }
    ```
    
    ```csharp
    // En: src/Infrastructure/Services/ReportService.cs
    // MODIFICAR GetInspectionHistoryAsync
    
    public async Task<IReadOnlyList<InspectionSummaryDto>> GetInspectionHistoryAsync(Guid organizationId)
    {
        // ... código existente ...
    
        var summaryList = inspections.Select(i => new InspectionSummaryDto
        (
            Id: i.Id,
            AnalysisDate: i.AnalysisDate,
            ImageUrl: i.ImageUrl,
            UserName: i.User.UserName ?? i.User.Email ?? $"Usuario {i.UserId}", // <-- ¡CORREGIDO!
            TotalFindings: i.Findings.Count
        )).ToList();
    
        return summaryList;
    }
    ```

---

## **PASO 34 (de 35): Habilitar Servicio de Archivos Estáticos en Program.cs**

* **EL QUÉ (La Tarea):**
    Añadimos el middleware de archivos estáticos para que las imágenes guardadas en `wwwroot/uploads` sean accesibles vía HTTP.

* **EL PARA QUÉ (El "Porqué"):**
    Sin esto, aunque guardemos las imágenes, el navegador no podrá acceder a ellas porque el servidor no las servirá como archivos estáticos.

* **EL CÓMO (El Código):**
    ```csharp
    // En: src/Api/Program.cs
    // Añadir después de app.UseHttpsRedirection() y ANTES de app.UseAuthentication()
    
    // 2. Redirección HTTPS (seguridad)
    app.UseHttpsRedirection();
    
    // 2.5. Habilitar archivos estáticos (para servir imágenes de wwwroot/uploads)
    app.UseStaticFiles(); // <-- ¡AÑADIDO!
    
    // 3. ¡VITAL! Habilitar Autenticación y Autorización
    app.UseAuthentication();
    ```

---

## **PASO 35 (de 35): Prueba de Fuego Final (Validación End-to-End)**

* **EL QUÉ (La Tarea):**
    Ejecutamos la API (F5) y probamos el flujo completo:
    1. Registro/Login de usuario
    2. Análisis de imagen con persistencia
    3. Consulta de historial
    4. Consulta de detalles
    5. Verificación de que la imagen se guardó y es accesible

* **EL PARA QUÉ (El "Porqué"):**
    Validar que todo el flujo funciona correctamente desde el frontend hasta la base de datos y el almacenamiento de archivos.

* **EL CÓMO (La Acción):**
    1. **Preparar una imagen de prueba:**
       - Toma una foto de un escenario con riesgos SST (ej. trabajador sin casco)
       - Conviértela a Base64 (puedes usar herramientas online o código)
    
    2. **Registrar/Login:**
       - `POST /api/auth/register` o `POST /api/auth/login`
       - Copiar el token JWT
    
    3. **Realizar análisis:**
       - `POST /api/v1/analysis/PerformSstAnalysis`
       - Headers: `Authorization: Bearer {token}`
       - Body: `{"ImageBase64": "...", "CustomPrompt": null}`
       - Verificar que devuelve `200 OK` con los hallazgos
    
    4. **Consultar historial:**
       - `GET /api/v1/analysis/history`
       - Headers: `Authorization: Bearer {token}`
       - Verificar que devuelve la inspección recién creada
    
    5. **Consultar detalles:**
       - `GET /api/v1/analysis/{inspectionId}`
       - Headers: `Authorization: Bearer {token}`
       - Verificar que devuelve todos los hallazgos
    
    6. **Verificar imagen:**
       - Abrir en navegador: `http://localhost:XXXX{ImageUrl}`
       - Verificar que la imagen se muestra correctamente

---

## **Resumen del Capítulo 5 Completado**

Al finalizar estos pasos, tendrás:

✅ **Sistema de Análisis de IA Completo:**
- GeminiAnalyzer funcional con manejo de errores robusto
- AnalysisService que orquesta análisis y persistencia
- Almacenamiento de imágenes (LocalFileStorage)
- Persistencia completa en base de datos (Inspection + Findings)

✅ **Sistema de Consultas y Reportes:**
- Historial de inspecciones por organización
- Detalles completos de inspecciones
- Filtrado Multi-Tenant correcto

✅ **Arquitectura Limpia Mantenida:**
- Contratos (interfaces) en Core
- Implementaciones en Infrastructure
- Desacoplamiento total entre capas

✅ **Listo para Producción:**
- Fácil migración a Azure Blob Storage (solo cambiar la implementación de IFileStorage)
- Configuración centralizada en appsettings.json
- Logging completo para debugging

---

## **Próximos Pasos (Capítulo 6 - Frontend MAUI)**

Una vez completado el Capítulo 5, el siguiente paso lógico es:

1. **Crear la App MAUI (VisioAnalytica.App.Risk):**
   - Pantalla de Login/Registro
   - Pantalla de captura de foto
   - Pantalla de resultados de análisis
   - Pantalla de historial de inspecciones

2. **Integración con la API:**
   - Cliente HTTP para llamar a los endpoints
   - Manejo de tokens JWT
   - Manejo de errores y estados de carga

3. **Mejoras de UX:**
   - Indicadores de carga
   - Mensajes de error amigables
   - Navegación fluida entre pantallas

