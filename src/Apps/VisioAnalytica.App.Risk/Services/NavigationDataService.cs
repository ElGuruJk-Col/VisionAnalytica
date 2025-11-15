using VisioAnalytica.App.Risk.Models;

namespace VisioAnalytica.App.Risk.Services;

/// <summary>
/// Implementación del servicio de datos de navegación.
/// Almacena datos temporalmente en memoria para evitar pasar datos grandes por URL.
/// </summary>
public class NavigationDataService : INavigationDataService
{
    private AnalysisResult? _storedResult;

    public void SetAnalysisResult(AnalysisResult result)
    {
        _storedResult = result;
    }

    public AnalysisResult? GetAndClearAnalysisResult()
    {
        var result = _storedResult;
        _storedResult = null; // Limpiar después de obtener
        return result;
    }
}

