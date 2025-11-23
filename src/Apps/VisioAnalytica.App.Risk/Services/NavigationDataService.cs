using VisioAnalytica.App.Risk.Models;

namespace VisioAnalytica.App.Risk.Services;

/// <summary>
/// Implementación del servicio de datos de navegación.
/// Almacena datos temporalmente en memoria para evitar pasar datos grandes por URL.
/// Este servicio es Singleton, por lo que los datos persisten entre navegaciones.
/// </summary>
public class NavigationDataService : INavigationDataService
{
    private AnalysisResult? _storedResult;
    private byte[]? _capturedImageBytes;
    private Guid? _affiliatedCompanyId;

    public void SetAnalysisResult(AnalysisResult result, byte[]? capturedImageBytes = null, Guid? affiliatedCompanyId = null)
    {
        _storedResult = result;
        _capturedImageBytes = capturedImageBytes; // Guardar copia de los bytes de la imagen
        _affiliatedCompanyId = affiliatedCompanyId;
    }

    public AnalysisResult? GetAndClearAnalysisResult()
    {
        var result = _storedResult;
        _storedResult = null; // Limpiar después de obtener
        _capturedImageBytes = null;
        _affiliatedCompanyId = null;
        return result;
    }

    public AnalysisResult? GetAnalysisResult()
    {
        // Obtener sin limpiar (para mantener caché)
        return _storedResult;
    }

    public byte[]? GetCapturedImageBytes()
    {
        // Obtener sin limpiar (para mantener caché)
        return _capturedImageBytes;
    }

    public Guid? GetAffiliatedCompanyId()
    {
        return _affiliatedCompanyId;
    }

    public void Clear()
    {
        _storedResult = null;
        _capturedImageBytes = null;
        _affiliatedCompanyId = null;
    }
}

