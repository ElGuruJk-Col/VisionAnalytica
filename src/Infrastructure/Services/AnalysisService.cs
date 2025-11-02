using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using VisioAnalytica.Core.Interfaces;
using VisioAnalytica.Core.Models;
using VisioAnalytica.Core.Models.Dtos;

namespace VisioAnalytica.Infrastructure.Services
{
    /// <summary>
    /// (v2.4 - Corregido logging estructurado CA2254 en constructor)
    /// </summary>
    public class AnalysisService : IAnalysisService
    {
        private readonly IAiSstAnalyzer _aiAnalyzer;
        private readonly ILogger<AnalysisService> _logger;
        private readonly string _masterSstPrompt;

        public AnalysisService(
            IAiSstAnalyzer aiAnalyzer,
            ILogger<AnalysisService> logger,
            IConfiguration configuration)
        {
            _aiAnalyzer = aiAnalyzer ?? throw new ArgumentNullException(nameof(aiAnalyzer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var promptKey = "AiPrompts:MasterSst";
            var prompt = configuration[promptKey];

            if (string.IsNullOrWhiteSpace(prompt))
            {
                // ¡CORRECCIÓN CA2254!
                // 1. Usamos una plantilla de log estática.
                const string errorTemplate = "¡ERROR CRÍTICO! El prompt '{PromptKey}' no se encontró o está vacío en la configuración (appsettings.json).";

                // 2. Pasamos el parámetro por separado.
                _logger.LogError(errorTemplate, promptKey);

                // 3. El 'throw' sí puede usar el string formateado.
                throw new InvalidOperationException($"¡ERROR CRÍTICO! El prompt '{promptKey}' no se encontró...");
            }

            _masterSstPrompt = prompt;
            _logger.LogInformation("AnalysisService inicializado y prompt maestro cargado.");
        }

        // ... (El resto del archivo 'PerformSstAnalysisAsync' se mantiene igual) ...
        public async Task<SstAnalysisResult?> PerformSstAnalysisAsync(AnalysisRequestDto request, string userId)
        {
            _logger.LogInformation("Iniciando PerformSstAnalysisAsync para el usuario {UserId}.", userId);
            string promptParaUsar;
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
            try
            {
                _logger.LogInformation("Convirtiendo imagen Base64 a byte[]...");
                var imageBytes = Convert.FromBase64String(request.ImageBase64);
                return await _aiAnalyzer.AnalyzeImageAsync(imageBytes, promptParaUsar);
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
        }
    }
}