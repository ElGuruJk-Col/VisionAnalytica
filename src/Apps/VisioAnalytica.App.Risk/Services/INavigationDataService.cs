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
    void SetAnalysisResult(AnalysisResult result, byte[]? capturedImageBytes = null, Guid? affiliatedCompanyId = null);

    /// <summary>
    /// Obtiene y elimina el resultado de análisis almacenado.
    /// </summary>
    AnalysisResult? GetAndClearAnalysisResult();

    /// <summary>
    /// Obtiene el resultado de análisis sin eliminarlo (para mantener caché).
    /// </summary>
    AnalysisResult? GetAnalysisResult();

    /// <summary>
    /// Obtiene los bytes de la imagen capturada (si están disponibles).
    /// </summary>
    byte[]? GetCapturedImageBytes();

    /// <summary>
    /// Obtiene el ID de la empresa afiliada asociada al análisis.
    /// </summary>
    Guid? GetAffiliatedCompanyId();

    /// <summary>
    /// Limpia todos los datos almacenados.
    /// </summary>
    void Clear();
}

