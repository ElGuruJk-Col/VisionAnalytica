using VisioAnalytica.App.Risk.Models;

namespace VisioAnalytica.App.Risk.Services;

/// <summary>
/// Servicio para almacenar temporalmente datos de navegación en memoria.
/// Útil para pasar datos grandes entre páginas sin usar parámetros de URL.
/// </summary>
public interface INavigationDataService
{
    /// <summary>
    /// Almacena temporalmente el resultado de un análisis.
    /// </summary>
    void SetAnalysisResult(AnalysisResult result);

    /// <summary>
    /// Obtiene y elimina el resultado de análisis almacenado.
    /// </summary>
    AnalysisResult? GetAndClearAnalysisResult();
}

