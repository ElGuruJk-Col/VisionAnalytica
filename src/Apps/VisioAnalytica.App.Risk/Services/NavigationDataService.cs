using VisioAnalytica.App.Risk.Models;

namespace VisioAnalytica.App.Risk.Services;

/// <summary>
/// Implementación del servicio de datos de navegación.
/// Almacena datos temporalmente en memoria para evitar pasar datos grandes por URL.
/// </summary>
public class NavigationDataService : INavigationDataService
{
    private AnalysisResult? _storedResult;
    private byte[]? _capturedImageBytes;

    public void SetAnalysisResult(AnalysisResult result, byte[]? capturedImageBytes = null)
    {
        _storedResult = result;
        _capturedImageBytes = capturedImageBytes; // Guardar copia de los bytes de la imagen
    }

    public AnalysisResult? GetAndClearAnalysisResult()
    {
        var result = _storedResult;
        _storedResult = null; // Limpiar después de obtener
        return result;
    }

    public byte[]? GetCapturedImageBytes()
    {
        var bytes = _capturedImageBytes;
        _capturedImageBytes = null; // Limpiar después de obtener
        return bytes;
    }
}

