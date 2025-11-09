using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VisioAnalytica.Core.Interfaces;
using VisioAnalytica.Core.Models;
using VisioAnalytica.Core.Models.Dtos;

namespace VisioAnalytica.Infrastructure.Services
{
    /// <summary>
    /// Servicio Orquestador de Análisis.
    /// Su responsabilidad es tomar la solicitud, interactuar con la IA y persistir
    /// los resultados en la base de datos a través del Repositorio.
    /// </summary>
    public class AnalysisService(
        IAiSstAnalyzer aiAnalyzer,
        IAnalysisRepository analysisRepository,
        ILogger<AnalysisService> logger,
        IConfiguration configuration) : IAnalysisService
    {
        private readonly IAiSstAnalyzer _aiAnalyzer = aiAnalyzer;
        private readonly IAnalysisRepository _analysisRepository = analysisRepository;
        private readonly ILogger<AnalysisService> _logger = logger;
        private readonly string _masterSstPrompt = GetMasterSstPrompt(configuration, logger);

        private static string GetMasterSstPrompt(IConfiguration configuration, ILogger logger)
        {
            var promptKey = "AiPrompts:MasterSst";
            var prompt = configuration[promptKey];

            if (string.IsNullOrWhiteSpace(prompt))
            {
                const string errorTemplate = "¡ERROR CRÍTICO! El prompt '{PromptKey}' no se encontró o está vacío en la configuración (appsettings.json).";
                logger.LogError(errorTemplate, promptKey);
                throw new InvalidOperationException($"¡ERROR CRÍTICO! El prompt '{promptKey}' no se encontró en la configuración.");
            }
            return prompt;
        }


        // --- LÓGICA DE NEGOCIO Y PERSISTENCIA COMBINADA ---

        // La firma del método ya recibe todos los GUIDs necesarios.
        public async Task<SstAnalysisResult?> PerformSstAnalysisAsync(AnalysisRequestDto request, string userId, Guid organizationId)
        {
            _logger.LogInformation("Iniciando PerformSstAnalysisAsync para el usuario {UserId} y Org {OrganizationId}.", userId, organizationId);
            string promptParaUsar;

            // 1. Determinar el Prompt a usar 
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
                _logger.LogInformation("Usando el prompt maestro de SST.");
                promptParaUsar = _masterSstPrompt;
            }

            SstAnalysisResult? result;

            try
            {
                // 2. Convertir y llamar a la IA
                _logger.LogInformation("Convirtiendo imagen Base64 a byte[] y llamando a la IA...");
                var imageBytes = Convert.FromBase64String(request.ImageBase64);

                result = await _aiAnalyzer.AnalyzeImageAsync(imageBytes, promptParaUsar);

                if (result == null)
                {
                    _logger.LogWarning("El conector IA (IAiSstAnalyzer) devolvió un resultado nulo.");
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

            // 3. Persistir el resultado en la BBDD (Capítulo 3)

            // 3.A: Construir la Inspección (cabecera)
            if (!Guid.TryParse(userId, out var parsedUserId))
            {
                // Esto solo se lanza si el validador del controlador falla
                _logger.LogError("Error de formato: El userId '{UserId}' no es un GUID válido.", userId);
                throw new InvalidOperationException("El identificador de usuario no tiene un formato válido (GUID). Fallo en la autenticación.");
            }

            var inspection = new Inspection
            {
                // ¡FIX FINAL! Usamos los GUIDs validados y pasados por argumento.
                UserId = parsedUserId,
                OrganizationId = organizationId,

                // TO-DO: Guardar la URL real de la imagen (Blob Storage)
                ImageUrl = "temp/image_b64_not_uploaded.jpg",
            };

            // 3.B: Mapear los Hallazgos (SstAnalysisResult) a las Entidades (Finding)
            foreach (var hallazgo in result.Hallazgos)
            {
                inspection.Findings.Add(new Finding
                {
                    Description = hallazgo.Descripcion,
                    RiskLevel = hallazgo.NivelRiesgo,
                    CorrectiveAction = hallazgo.AccionCorrectiva,
                    PreventiveAction = hallazgo.AccionPreventiva,
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
                throw;
            }

            // 4. Devolver el resultado de la IA al Controller API.
            return result;
        }
    }
}