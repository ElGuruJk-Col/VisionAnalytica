using VisioAnalytica.Core.Interfaces;
using VisioAnalytica.Core.Models;

namespace VisioAnalytica.Infrastructure.Services
{
    /// <summary>
    /// Implementación "Dummy" (de relleno) del analizador de IA.
    /// Su único propósito en esta rama (auth-foundation) es
    /// compilar y permitir que el Program.cs registre el servicio.
    /// NO se conecta a ninguna IA real... todavía.
    /// </summary>
    public class GeminiAnalyzer : IAiSstAnalyzer
    {
        /// <summary>
        /// Implementa el contrato de la interfaz, pero devuelve
        /// datos de prueba (harcoded) en lugar de llamar a una API.
        /// </summary>
        public Task<SstAnalysisResult> AnalyzeImageAsync(byte[] imageBytes, string prompt)
        {
            // NOTA: Esta es una implementación "dummy" (falsa).
            // No llama a ninguna IA. Solo devuelve datos de prueba.
            // En la rama "feature/ai-connector" construiremos la lógica real.

            var dummyResult = new SstAnalysisResult
            {
                Hallazgo = "Dato de prueba: Casco de seguridad mal puesto.",
                SolucionPropuesta = "Dato de prueba: Ajustar el arnés del casco.",
                NivelRiesgo = "Medio",
                ReferenciaLegal = "Dato de prueba: Art. 123"
            };

            // Usamos Task.FromResult para devolver un Task completado
            // con nuestro resultado falso, cumpliendo así el contrato async.
            return Task.FromResult(dummyResult);
        }
    }
}

