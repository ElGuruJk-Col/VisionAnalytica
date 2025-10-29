namespace VisioAnalytica.Core.Models
{
    /// <summary>
    /// Modelo de datos estándar (DTO) para la respuesta
    /// del análisis de IA.
    /// Definido en Core para ser usado por todos los proyectos.
    /// </summary>
    public class SstAnalysisResult
    {
        /// <summary>
        /// El riesgo o hallazgo específico identificado por la IA.
        /// (Ej: "Cableado expuesto en zona de paso")
        /// </summary>
        public string Hallazgo { get; set; } = null!;

        /// <summary>
        /// La acción correctiva o solución propuesta por la IA.
        /// (Ej: "Canalizar inmediatamente y señalizar")
        /// </summary>
        public string SolucionPropuesta { get; set; } = null!;

        /// <summary>
        /// El nivel de riesgo (Alto, Medio, Bajo)
        /// </summary>
        public string NivelRiesgo { get; set; } = null!;

        /// <summary>
        /// (Opcional, para v2) La referencia legal o normativa
        /// que se está incumpliendo.
        /// </summary>
        public string? ReferenciaLegal { get; set; }
    }
}

