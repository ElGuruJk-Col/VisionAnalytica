// En: src/Infrastructure/Services/AnalysisService.cs
// (v3.0 - ¡IMPLEMENTACIÓN DE PERSISTENCIA!)

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic; // Necesario para List/ICollection
using System.Security.Claims; // Necesario para ClaimTypes (si no se usa User.FindFirstValue)
using System.Threading.Tasks;
using VisioAnalytica.Core.Interfaces;
using VisioAnalytica.Core.Models;
using VisioAnalytica.Core.Models.Dtos;

namespace VisioAnalytica.Infrastructure.Services
{
    /// <summary>
    /// (v3.0 - Actualizado con inyección de IAnalysisRepository)
    /// </summary>
    public class AnalysisService : IAnalysisService
    {
        private readonly IAiSstAnalyzer _aiAnalyzer;
        private readonly IAnalysisRepository _analysisRepository; // << ¡NUEVO CAMPO!
        private readonly ILogger<AnalysisService> _logger;
        private readonly string _masterSstPrompt;

        public AnalysisService(
            IAiSstAnalyzer aiAnalyzer,
            IAnalysisRepository analysisRepository, // << ¡NUEVA INYECCIÓN!
            ILogger<AnalysisService> logger,
            IConfiguration configuration)
        {
            _aiAnalyzer = aiAnalyzer ?? throw new ArgumentNullException(nameof(aiAnalyzer));
            _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository)); // << ASIGNACIÓN
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var promptKey = "AiPrompts:MasterSst";
            var prompt = configuration[promptKey];

            if (string.IsNullOrWhiteSpace(prompt))
            {
                const string errorTemplate = "¡ERROR CRÍTICO! El prompt '{PromptKey}' no se encontró o está vacío en la configuración (appsettings.json).";
                _logger.LogError(errorTemplate, promptKey);
                throw new InvalidOperationException($"¡ERROR CRÍTICO! El prompt '{promptKey}' no se encontró...");
            }

            _masterSstPrompt = prompt;
            _logger.LogInformation("AnalysisService inicializado y prompt maestro cargado.");
        }

        // --- LÓGICA DE NEGOCIO Y PERSISTENCIA COMBINADA ---

        public async Task<SstAnalysisResult?> PerformSstAnalysisAsync(AnalysisRequestDto request, string userId)
        {
            _logger.LogInformation("Iniciando PerformSstAnalysisAsync para el usuario {UserId}.", userId);
            string promptParaUsar;

            // 1. Determinar el Prompt (Lógica existente)
            if (!string.IsNullOrWhiteSpace(request.CustomPrompt))
            {
                _logger.LogWarning("Usando prompt personalizado para el usuario {UserId}.", userId);
                promptParaUsar = request.CustomPrompt;
            }
            else if (request.PromptTemplateId.HasValue)
            {
                _logger.LogInformation("Usando plantilla de prompt ID {TemplateId} (Lógica futura, usando maestro por ahora).", request.PromptTemplateId.Value);
                promptParaUsar = _masterSstPrompt;
            }
            else
            {
                _logger.LogInformation("Usando el prompt maestro de SST (nuestro modelo) cargado desde config.");
                promptParaUsar = _masterSstPrompt;
            }

            SstAnalysisResult? result;

            try
            {
                // 2. Convertir y llamar a la IA (Lógica existente)
                _logger.LogInformation("Convirtiendo imagen Base64 a byte[] y llamando a la IA...");
                var imageBytes = Convert.FromBase64String(request.ImageBase64);
                result = await _aiAnalyzer.AnalyzeImageAsync(imageBytes, promptParaUsar);

                if (result == null)
                {
                    _logger.LogWarning("El conector IA (GeminiAnalyzer) devolvió un resultado nulo.");
                    return null;
                }
            }
            catch (FormatException ex)
            {
                _logger.LogWarning(ex, "El string Base64 recibido del usuario {UserId} no es válido. No se puede convertir.", userId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en el conector IA (IAiSstAnalyzer) llamado por AnalysisService para el usuario {UserId}.", userId);
                throw;
            }

            // 3. ¡NUEVA LÓGICA! Persistir el resultado en la BBDD

            // NOTA: Asumimos que el token JWT contiene la OrganizationId y que
            // la API (AnalysisController) la extrae y la pasa a este servicio,
            // pero como no se ha modificado la firma, debemos asumir un placeholder
            // o que la OrganizationId puede extraerse del User.
            // Por ahora, usaremos un placeholder (Guid.Empty) que debe ser corregido
            // cuando implementemos el token en AnalysisController.

            // 3.A: Construir la Inspección (cabecera)
            var inspection = new Inspection
            {
                UserId = Guid.Parse(userId), // Convertimos el string de userId (Guid)
                // TO-DO: Necesitamos la OrganizationId del token del usuario para esto
                // Usaremos un placeholder que debe ser válido (ej. Guid.NewGuid() si no la tenemos)
                OrganizationId = Guid.NewGuid(), // <-- CORREGIR CUANDO SE LEA DEL TOKEN!

                // TO-DO: Guardar la URL real de la imagen (Blob Storage)
                ImageUrl = "temp/image_b64_not_uploaded.jpg", // <-- CORREGIR CON LÓGICA DE BLOB
            };

            // 3.B: Mapear los Hallazgos de la IA (SstAnalysisResult) a las Entidades (Finding)
            foreach (var hallazgo in result.Hallazgos)
            {
                inspection.Findings.Add(new Finding
                {
                    Description = hallazgo.Descripcion,
                    RiskLevel = hallazgo.NivelRiesgo,
                    CorrectiveAction = hallazgo.AccionCorrectiva,
                    PreventiveAction = hallazgo.AccionPreventiva,
                    // InspectionId se asigna automáticamente al hacer Add a la colección de Findings.
                });
            }

            try
            {
                // 3.C: Guardar en el Repositorio
                await _analysisRepository.SaveInspectionAsync(inspection);
                _logger.LogInformation("Inspección {InspectionId} persistida en la BBDD para el usuario {UserId}.", inspection.Id, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CRÍTICO al guardar la Inspección en el repositorio para el usuario {UserId}. El resultado de la IA se perdió.", userId);
                // Si la persistencia falla, lanzamos el error, pero el resultado de la IA (result) ya existe.
                // Podríamos decidir devolver 'result' y solo loguear el fallo de persistencia,
                // pero por ahora, la persistencia es crítica.
                throw;
            }

            // 4. Devolver el resultado de la IA (que es lo que espera el cliente API)
            return result;
        }
    }
}