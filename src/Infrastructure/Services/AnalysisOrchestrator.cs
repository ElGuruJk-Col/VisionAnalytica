using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VisioAnalytica.Core.Interfaces;
using VisioAnalytica.Core.Models;
using VisioAnalytica.Core.Models.Dtos;
using VisioAnalytica.Infrastructure.Data;

namespace VisioAnalytica.Infrastructure.Services;

/// <summary>
/// Orquestador que procesa el análisis de múltiples fotos en segundo plano.
/// </summary>
public class AnalysisOrchestrator(
    VisioAnalyticaDbContext context,
    IAnalysisService analysisService,
    IFileStorage fileStorage,
    IEmailService emailService,
    IPdfReportGenerator pdfReportGenerator,
    ILogger<AnalysisOrchestrator> logger) : IAnalysisOrchestrator
{
    private readonly VisioAnalyticaDbContext _context = context;
    private readonly IAnalysisService _analysisService = analysisService;
    private readonly IFileStorage _fileStorage = fileStorage;
    private readonly IEmailService _emailService = emailService;
    private readonly IPdfReportGenerator _pdfReportGenerator = pdfReportGenerator;
    private readonly ILogger<AnalysisOrchestrator> _logger = logger;

    public async Task AnalyzeInspectionPhotosAsync(Guid inspectionId, List<Guid> photoIds, Guid userId)
    {
        var jobId = Guid.NewGuid();
        _logger.LogInformation(
            "🔵 [AnalysisOrchestrator] JOB INICIADO - JobId: {JobId}, InspectionId: {InspectionId}, PhotoCount: {PhotoCount}, UserId: {UserId}, Time: {Time}",
            jobId, inspectionId, photoIds.Count, userId, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        
        try
        {
            _logger.LogInformation("Iniciando análisis de inspección {InspectionId} con {PhotoCount} fotos", 
                inspectionId, photoIds.Count);

            // Obtener la inspección (asegurar que está siendo rastreada)
            var inspection = await _context.Inspections
                .Include(i => i.User)
                .Include(i => i.AffiliatedCompany)
                .Include(i => i.Photos)
                .Include(i => i.Findings) // ⚠️ Incluir Findings para evitar problemas de tracking
                .FirstOrDefaultAsync(i => i.Id == inspectionId);

            if (inspection == null)
            {
                _logger.LogError("Inspección {InspectionId} no encontrada", inspectionId);
                return;
            }

            _logger.LogInformation(
                "Inspección {InspectionId} cargada. Estado actual: {Status}, Hallazgos existentes: {FindingsCount}",
                inspectionId, inspection.Status, inspection.Findings?.Count ?? 0);

            // Verificar que la inspección está siendo rastreada (no debería ser null)
            var entry = _context.Entry(inspection);
            _logger.LogInformation(
                "Estado de tracking de inspección {InspectionId}: {State}",
                inspectionId, entry.State);

            // Actualizar estado a "Analyzing" (guardar inmediatamente para marcar inicio)
            inspection.Status = "Analyzing";
            var initialSave = await _context.SaveChangesAsync();
            _logger.LogInformation(
                "Estado de inspección {InspectionId} actualizado a 'Analyzing'. Entidades afectadas: {Entries}",
                inspectionId, initialSave);

            var analyzedCount = 0;
            var failedCount = 0;

            // Analizar cada foto seleccionada
            foreach (var photoId in photoIds)
            {
                try
                {
                    var photo = inspection.Photos.FirstOrDefault(p => p.Id == photoId);
                    if (photo == null || photo.IsAnalyzed)
                    {
                        _logger.LogWarning("Foto {PhotoId} no encontrada o ya analizada", photoId);
                        continue;
                    }

                    // Leer la imagen desde el almacenamiento
                    var imageBytes = await _fileStorage.ReadImageAsync(photo.ImageUrl);
                    if (imageBytes == null || imageBytes.Length == 0)
                    {
                        _logger.LogError("No se pudo leer la imagen de la foto {PhotoId}", photoId);
                        failedCount++;
                        continue;
                    }

                    // Convertir a Base64 para el análisis
                    var imageBase64 = Convert.ToBase64String(imageBytes);

                    // Crear request de análisis
                    var analysisRequest = new AnalysisRequestDto(
                        imageBase64,
                        null, // No usar prompt personalizado por ahora
                        null
                    );

                    // Realizar análisis (skipPersistence = true para evitar crear inspecciones duplicadas)
                    var analysisResult = await _analysisService.PerformSstAnalysisAsync(
                        analysisRequest,
                        userId.ToString(),
                        inspection.OrganizationId,
                        skipPersistence: true); // ⚠️ IMPORTANTE: No crear inspección aquí, solo obtener resultados

                    if (analysisResult != null)
                    {
                        // ═══════════════════════════════════════════════════════════════
                        // CORRECCIÓN: Agregar hallazgos directamente a la inspección original
                        // NO crear nuevas inspecciones
                        // ═══════════════════════════════════════════════════════════════
                        
                        // Agregar hallazgos directamente a la inspección original
                        if (analysisResult.Hallazgos != null && analysisResult.Hallazgos.Count > 0)
                        {
                            // ⚠️ CRÍTICO: Asegurar que la inspección está siendo rastreada como Modified, no Added
                            var inspectionEntry = _context.Entry(inspection);
                            if (inspectionEntry.State == Microsoft.EntityFrameworkCore.EntityState.Detached)
                            {
                                _logger.LogError("ERROR CRÍTICO: La inspección {InspectionId} está en estado Detached. Re-attaching...", inspection.Id);
                                _context.Attach(inspection);
                                inspectionEntry.State = Microsoft.EntityFrameworkCore.EntityState.Modified; // ⚠️ Marcar como Modified, no Added
                            }
                            else if (inspectionEntry.State == Microsoft.EntityFrameworkCore.EntityState.Added)
                            {
                                _logger.LogError("ERROR CRÍTICO: La inspección {InspectionId} está en estado Added. Cambiando a Modified...", inspection.Id);
                                inspectionEntry.State = Microsoft.EntityFrameworkCore.EntityState.Modified; // ⚠️ Cambiar a Modified
                            }
                            
                            _logger.LogInformation(
                                "Agregando {Count} hallazgos a la inspección {InspectionId}. Estado: {State}, Hallazgos actuales antes: {CurrentCount}",
                                analysisResult.Hallazgos.Count, inspection.Id, inspectionEntry.State, inspection.Findings?.Count ?? 0);
                            
                            // ⚠️ CRÍTICO: Agregar hallazgos directamente al contexto en lugar de a través de la colección de navegación
                            // Esto evita que Entity Framework marque la inspección como nueva
                            foreach (var hallazgo in analysisResult.Hallazgos)
                            {
                                var finding = new Finding
                                {
                                    Id = Guid.NewGuid(),
                                    InspectionId = inspection.Id, // ⚠️ Usar la inspección original
                                    Description = hallazgo.Descripcion,
                                    RiskLevel = hallazgo.NivelRiesgo,
                                    CorrectiveAction = hallazgo.AccionCorrectiva,
                                    PreventiveAction = hallazgo.AccionPreventiva
                                };
                                
                                // ⚠️ Agregar directamente al contexto, NO a través de la colección de navegación
                                _context.Findings.Add(finding);
                                
                                _logger.LogDebug(
                                    "Hallazgo {FindingId} agregado directamente al contexto para inspección {InspectionId}: {Description}",
                                    finding.Id, inspection.Id, finding.Description);
                            }
                            
                            _logger.LogInformation(
                                "Agregados {Count} hallazgos directamente al contexto para inspección {InspectionId}",
                                analysisResult.Hallazgos.Count, inspection.Id);
                        }

                        // Marcar la foto como analizada (sin crear nueva inspección)
                        photo.IsAnalyzed = true;
                        photo.AnalysisInspectionId = null; // ⚠️ Ya no necesitamos referenciar otra inspección
                        
                        // ⚠️ Verificar estado antes de guardar
                        var inspectionStateBeforeSave = _context.Entry(inspection).State;
                        var findingsCountBeforeSave = inspection.Findings?.Count ?? 0;
                        var inspectionIdBeforeSave = inspection.Id;
                        _logger.LogInformation(
                            "Estado de inspección {InspectionId} antes de SaveChanges: State={State}, Hallazgos={FindingsCount}",
                            inspection.Id, inspectionStateBeforeSave, findingsCountBeforeSave);
                        
                        // ⚠️ Verificar que no hay inspecciones duplicadas antes de guardar
                        var inspectionCountBeforeSave = await _context.Inspections.CountAsync(i => i.Id == inspection.Id);
                        if (inspectionCountBeforeSave > 1)
                        {
                            _logger.LogError(
                                "❌ ERROR CRÍTICO ANTES DE GUARDAR: Ya existen {Count} inspecciones con ID {InspectionId}",
                                inspectionCountBeforeSave, inspection.Id);
                        }
                        
                        // Guardar cambios en una sola transacción
                        var savedEntries = await _context.SaveChangesAsync();
                        
                        // ⚠️ Verificar que el ID de la inspección no cambió (no se creó una nueva)
                        if (inspection.Id != inspectionIdBeforeSave)
                        {
                            _logger.LogError(
                                "❌ ERROR CRÍTICO: El ID de la inspección cambió de {OldId} a {NewId}. Se creó una nueva inspección!",
                                inspectionIdBeforeSave, inspection.Id);
                        }
                        
                        _logger.LogInformation(
                            "SaveChanges completado para inspección {InspectionId}. Entidades afectadas: {SavedEntries}, Hallazgos después: {FindingsCount}",
                            inspection.Id, savedEntries, inspection.Findings?.Count ?? 0);
                        
                        // Verificar que no se crearon nuevas inspecciones después de guardar
                        var inspectionCountAfterSave = await _context.Inspections.CountAsync(i => i.Id == inspection.Id);
                        if (inspectionCountAfterSave > 1)
                        {
                            _logger.LogError(
                                "❌ ERROR CRÍTICO DESPUÉS DE GUARDAR: Se detectaron {Count} inspecciones con el mismo ID {InspectionId}",
                                inspectionCountAfterSave, inspection.Id);
                        }
                        else if (inspectionCountAfterSave == 1)
                        {
                            _logger.LogInformation(
                                "✅ Verificación OK: Solo existe 1 inspección con ID {InspectionId}",
                                inspection.Id);
                        }
                        
                        // Verificar que el cambio se guardó
                        var updatedPhoto = await _context.Photos.FindAsync(photoId);
                        if (updatedPhoto != null)
                        {
                            _logger.LogInformation(
                                "Foto {PhotoId} marcada como analizada. IsAnalyzed={IsAnalyzed}, AnalysisInspectionId={AnalysisId}",
                                photoId, updatedPhoto.IsAnalyzed, updatedPhoto.AnalysisInspectionId);
                        }
                        else
                        {
                            _logger.LogError("ERROR: No se pudo verificar la foto {PhotoId} después de marcarla como analizada", photoId);
                        }

                        analyzedCount++;
                        _logger.LogInformation("Foto {PhotoId} analizada exitosamente", photoId);
                    }
                    else
                    {
                        failedCount++;
                        _logger.LogWarning("El análisis de la foto {PhotoId} no devolvió resultados", photoId);
                    }
                }
                catch (Exception ex)
                {
                    failedCount++;
                    _logger.LogError(ex, "Error al analizar foto {PhotoId}", photoId);
                }
            }

            // Recargar la inspección para verificar el estado actual de las fotos
            await _context.Entry(inspection).Collection(i => i.Photos).LoadAsync();
            var totalPhotos = inspection.Photos.Count;
            var analyzedPhotos = inspection.Photos.Count(p => p.IsAnalyzed);
            
            _logger.LogInformation(
                "Estado de fotos en inspección {InspectionId}: {TotalPhotos} totales, {AnalyzedPhotos} analizadas",
                inspectionId, totalPhotos, analyzedPhotos);

            // Actualizar estado de la inspección
            if (analyzedCount > 0 && failedCount == 0)
            {
                inspection.Status = "Completed";
            }
            else if (analyzedCount > 0 && failedCount > 0)
            {
                inspection.Status = "Completed"; // Parcialmente completada
            }
            else
            {
                inspection.Status = "Failed";
            }

            inspection.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "✅ [AnalysisOrchestrator] JOB COMPLETADO - JobId: {JobId}, InspectionId: {InspectionId}. Exitosos: {AnalyzedCount}, Fallidos: {FailedCount}. Estado final: {Status}",
                jobId, inspectionId, analyzedCount, failedCount, inspection.Status);
            
            // ⚠️ Verificación final: contar inspecciones con este ID
            var finalInspectionCount = await _context.Inspections.CountAsync(i => i.Id == inspectionId);
            if (finalInspectionCount != 1)
            {
                _logger.LogError(
                    "❌ [AnalysisOrchestrator] ERROR CRÍTICO - JobId: {JobId}, InspectionId: {InspectionId}. Se detectaron {Count} inspecciones con el mismo ID después de completar el análisis",
                    jobId, inspectionId, finalInspectionCount);
            }
            else
            {
                _logger.LogInformation(
                    "✅ [AnalysisOrchestrator] Verificación OK - JobId: {JobId}, InspectionId: {InspectionId}. Solo existe 1 inspección con este ID",
                    jobId, inspectionId);
            }

            // Enviar notificación por email
            if (inspection.User != null && !string.IsNullOrEmpty(inspection.User.Email))
            {
                try
                {
                    // Generar reporte PDF
                    byte[]? pdfBytes = null;
                    try
                    {
                        // Recargar inspección con TODOS los datos necesarios para el reporte
                        // ⚠️ CORRECCIÓN: Los hallazgos ahora están directamente en la inspección original
                        var fullInspection = await _context.Inspections
                            .Include(i => i.User)
                            .Include(i => i.AffiliatedCompany)
                            .Include(i => i.Photos)
                            .Include(i => i.Findings) // ⚠️ Hallazgos directamente en la inspección
                            .FirstOrDefaultAsync(i => i.Id == inspectionId);

                        if (fullInspection != null)
                        {
                            pdfBytes = _pdfReportGenerator.GenerateInspectionReport(fullInspection);
                            _logger.LogInformation("Reporte PDF generado para inspección {InspectionId}. Tamaño: {Size} bytes", inspectionId, pdfBytes.Length);
                        }
                    }
                    catch (Exception pdfEx)
                    {
                        _logger.LogError(pdfEx, "Error al generar reporte PDF para inspección {InspectionId}", inspectionId);
                    }

                    var companyName = inspection.AffiliatedCompany?.Name ?? "Empresa Cliente";
                    
                    // Preparar adjuntos
                    var attachments = new Dictionary<string, byte[]>();
                    if (pdfBytes != null)
                    {
                        attachments.Add($"Reporte_Inspeccion_{inspection.StartedAt:yyyyMMdd}.pdf", pdfBytes);
                    }

                    // Enviar email con adjunto
                    var subject = $"Análisis Completado - {companyName}";
                    var body = EmailTemplates.GetAnalysisCompleteTemplate(companyName, inspectionId);

                    var message = new EmailMessage
                    {
                        To = inspection.User.Email,
                        Subject = subject,
                        Body = body,
                        IsHtml = true,
                        Attachments = attachments
                    };

                    await _emailService.SendEmailAsync(message);
                    
                    _logger.LogInformation("Email de notificación enviado a {Email} con reporte adjunto", inspection.User.Email);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al enviar email de notificación para inspección {InspectionId}", inspectionId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crítico al procesar análisis de inspección {InspectionId}", inspectionId);
            
            // Actualizar estado a "Failed"
            try
            {
                var inspection = await _context.Inspections.FindAsync(inspectionId);
                if (inspection != null)
                {
                    inspection.Status = "Failed";
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Error al actualizar estado de inspección {InspectionId} a Failed", inspectionId);
            }
        }
    }
}

