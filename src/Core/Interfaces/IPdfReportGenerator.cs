using VisioAnalytica.Core.Models;

namespace VisioAnalytica.Core.Interfaces
{
    /// <summary>
    /// Interfaz para la generación de reportes PDF.
    /// </summary>
    public interface IPdfReportGenerator
    {
        /// <summary>
        /// Genera un reporte PDF de inspección.
        /// </summary>
        /// <param name="inspection">La inspección completa con fotos y hallazgos.</param>
        /// <returns>Bytes del archivo PDF generado.</returns>
        byte[] GenerateInspectionReport(Inspection inspection);
    }
}
